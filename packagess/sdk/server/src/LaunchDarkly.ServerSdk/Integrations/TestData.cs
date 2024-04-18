﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// A mechanism for providing dynamically updatable feature flag state in a simplified form to an SDK
    /// client in test scenarios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="FileData"/>, this mechanism does not use any external resources. It provides only
    /// the data that the application has put into it using the <see cref="Update(TestData.FlagBuilder)"/> method.
    /// </para>
    /// <para>
    /// The example code below uses a simple boolean flag, but more complex configurations are possible using
    /// the methods of the <see cref="FlagBuilder"/> that is returned by <see cref="Flag(string)"/>.
    /// <see cref="FlagBuilder"/> supports many of the ways a flag can be configured on the LaunchDarkly
    /// dashboard, but does not currently support 1. rule operators other than "in" and "not in", or 2.
    /// percentage rollouts.
    /// </para>
    /// <para>
    /// If the same <see cref="TestData"/> instance is used to configure multiple <see cref="LdClient"/>
    /// instances, any changes made to the data will propagate to all of the <see cref="LdClient"/>s.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    ///     var td = TestData.DataSource();
    ///     td.Update(td.Flag("flag-key-1").BooleanFlag().VariationForAll(true));
    ///
    ///     var config = Configuration.Builder("sdk-key")
    ///         .DataSource(td)
    ///         .Build();
    ///     var client = new LdClient(config);
    ///
    ///     // flags can be updated at any time:
    ///     td.update(testData.flag("flag-key-2")
    ///         .VariationForUser("some-user-key", true)
    ///         .FallthroughVariation(false));
    /// </code>
    /// </example>
    /// <seealso cref="FileData"/>
    public sealed class TestData : IComponentConfigurer<IDataSource>
    {
        #region Private fields

        private readonly object _lock = new object();
        private readonly Dictionary<string, ItemDescriptor> _currentFlags =
            new Dictionary<string, ItemDescriptor>();
        private readonly Dictionary<string, ItemDescriptor> _currentSegments =
            new Dictionary<string, ItemDescriptor>();
        private readonly Dictionary<string, FlagBuilder> _currentBuilders =
            new Dictionary<string, FlagBuilder>();
        private readonly List<DataSourceImpl> _instances = new List<DataSourceImpl>();

        #endregion

        #region Private constructor

        private TestData() { }

        #endregion

        #region Public methods

        /// <summary>
        /// Creates a new instance of the test data source.
        /// </summary>
        /// <remarks>
        /// See <see cref="TestData"/> for details.
        /// </remarks>
        /// <returns>a new configurable test data source</returns>
        public static TestData DataSource() => new TestData();

        /// <summary>
        /// Creates or copies a <see cref="FlagBuilder"/> for building a test flag configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If this flag key has already been defined in this <see cref="TestData"/> instance, then
        /// the builder starts with the same configuration that was last provided for this flag.
        /// </para>
        /// <para>
        /// Otherwise, it starts with a new default configuration in which the flag has <c>true</c>
        /// and <c>false</c> variations, is <c>true</c> for all contexts when targeting is turned on
        /// and <c>false</c> otherwise, and currently has targeting turned on. You can change any
        /// of those properties, and provide more complex behavior, using the
        /// <see cref="FlagBuilder"/> methods.
        /// </para>
        /// <para>
        /// Once you have set the desired configuration, pass the builder to
        /// <see cref="Update(FlagBuilder)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the flag key</param>
        /// <returns>a flag configuration builder</returns>
        /// <seealso cref="Update(FlagBuilder)"/>
        public FlagBuilder Flag(string key)
        {
            FlagBuilder existingBuilder;
            lock (_lock)
            {
                _currentBuilders.TryGetValue(key, out existingBuilder);
            }
            if (existingBuilder != null)
            {
                return new FlagBuilder(existingBuilder);
            }
            return new FlagBuilder(key).BooleanFlag();
        }

        /// <summary>
        /// Updates the test data with the specified flag configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This has the same effect as if a flag were added or modified on the LaunchDarkly dashboard.
        /// It immediately propagates the flag change to any <see cref="LdClient"/> instance(s) that
        /// you have already configured to use this <see cref="TestData"/>. If no <see cref="LdClient"/>
        /// has been started yet, it simply adds this flag to the test data which will be provided to any
        /// <see cref="LdClient"/> that you subsequently configure.
        /// </para>
        /// <para>
        /// Any subsequent changes to this <see cref="FlagBuilder"/> instance do not affect the test data,
        /// unless you call <see cref="Update(FlagBuilder)"/> again.
        /// </para>
        /// </remarks>
        /// <param name="flagBuilder">a flag configuration builder</param>
        /// <returns>the same <see cref="TestData"/> instance</returns>
        /// <seealso cref="Flag(string)"/>
        public TestData Update(FlagBuilder flagBuilder)
        {
            var key = flagBuilder._key;
            var clonedBuilder = new FlagBuilder(flagBuilder);
            UpdateInternal(key, flagBuilder.CreateFlag, clonedBuilder);
            return this;
        }

        private void UpdateInternal(string key, Func<int, ItemDescriptor> makeFlag, FlagBuilder builder)
        {
            ItemDescriptor newItem;
            DataSourceImpl[] instances;

            lock (_lock)
            {
                var oldVersion = _currentFlags.TryGetValue(key, out var oldItem) ?
                    oldItem.Version : 0;
                newItem = makeFlag(oldVersion + 1);
                _currentFlags[key] = newItem;
                if (builder is null)
                {
                    _currentBuilders.Remove(key);
                }
                else
                {
                    _currentBuilders[key] = builder;
                }
                instances = _instances.ToArray();
            }

            foreach (var instance in instances)
            {
                instance.DoUpdate(DataModel.Features, key, newItem);
            }
        }

        /// <summary>
        /// For SDK tests only - inserts a full feature flag data model object into the test data.
        /// </summary>
        /// <remarks>
        /// This fully replaces any existing flag with the same key, and immediately propagates the change
        /// to any LdClient instance(s) using the data source.
        /// </remarks>
        /// <param name="flag">a flag instance</param>
        /// <returns>the same <see cref="TestData"/> instance</returns>
        internal TestData UsePreconfiguredFlag(FeatureFlag flag)
        {
            UpdateInternal(flag.Key,
                version =>
                {
                    if (flag.Version < version)
                    {
                        flag = new FeatureFlag(flag.Key,
                            version,
                            flag.Deleted, flag.On, flag.Prerequisites, flag.Targets, null, flag.Rules,
                            flag.Fallthrough, flag.OffVariation, flag.Variations, flag.Salt,
                            flag.TrackEvents, flag.TrackEventsFallthrough, flag.DebugEventsUntilDate, flag.ClientSide,
                            flag.SamplingRatio, flag.ExcludeFromSummaries, flag.Migration);
                    }
                    return new ItemDescriptor(flag.Version, flag);
                },
                null);
            return this;
        }

        internal TestData UsePreconfiguredSegment(Segment segment)
        {
            ItemDescriptor newItem;
            DataSourceImpl[] instances;

            lock (_lock)
            {
                var oldVersion = _currentSegments.TryGetValue(segment.Key, out var oldItem) ?
                    oldItem.Version : 0;
                var newVersion = oldVersion + 1;
                if (segment.Version < newVersion)
                {
                    segment = new Segment(segment.Key,
                        newVersion,
                        segment.Deleted, segment.Included, segment.Excluded, segment.IncludedContexts, segment.ExcludedContexts,
                        segment.Rules, segment.Salt, segment.Unbounded, segment.UnboundedContextKind, segment.Generation);
                }
                newItem = new ItemDescriptor(newVersion, segment);
                _currentSegments[segment.Key] = newItem;
                instances = _instances.ToArray();
            }

            foreach (var instance in instances)
            {
                instance.DoUpdate(DataModel.Segments, segment.Key, newItem);
            }

            return this;
        }

        /// <summary>
        /// Simulates a change in the data source status.
        /// </summary>
        /// <remarks>
        /// Use this if you want to test the behavior of application code that uses
        /// <see cref="ILdClient.DataSourceStatusProvider"/> to track whether the data source is having
        /// problems (for example, a network failure interrupting the streaming connection). It does
        /// not actually stop the <see cref="TestData"/> data source from working, so even if you have
        /// simulated an outage, calling <see cref="Update(FlagBuilder)"/> will still send updates.
        /// </remarks>
        /// <param name="newState">one of the constants defined by <see cref="DataSourceState"/></param>
        /// <param name="newError">an optional <see cref="DataSourceStatus.ErrorInfo"/> instance</param>
        /// <returns></returns>
        public TestData UpdateStatus(DataSourceState newState, DataSourceStatus.ErrorInfo? newError)
        {
            DataSourceImpl[] instances;
            lock (_lock)
            {
                instances = _instances.ToArray();
            }
            foreach (var instance in instances)
            {
                instance.DoUpdateStatus(newState, newError);
            }
            return this;
        }

        /// <inheritdoc/>/>
        public IDataSource Build(LdClientContext context)
        {
            var instance = new DataSourceImpl(this, context.DataSourceUpdates, context.Logger.SubLogger("DataSource.TestData"));
            lock (_lock)
            {
                _instances.Add(instance);
            }
            return instance;
        }

        internal FullDataSet<ItemDescriptor> MakeInitData()
        {
            lock (_lock)
            {
                var b = ImmutableList.CreateBuilder<KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>>();
                b.Add(new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(
                    DataModel.Features,
                    new KeyedItems<ItemDescriptor>(_currentFlags.ToImmutableDictionary())
                    ));
                b.Add(new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(
                    DataModel.Segments,
                    new KeyedItems<ItemDescriptor>(_currentSegments.ToImmutableDictionary())
                    ));
                return new FullDataSet<ItemDescriptor>(b.ToImmutable());
            }
        }

        internal void ClosedInstance(DataSourceImpl instance)
        {
            lock (_lock)
            {
                _instances.Remove(instance);
            }
        }

        #endregion

        #region Public inner types

        /// <summary>
        /// A builder for feature flag configurations to be used with <see cref="TestData"/>.
        /// </summary>
        /// <seealso cref="TestData.Flag(string)"/>
        /// <seealso cref="TestData.Update(FlagBuilder)"/>
        public sealed class FlagBuilder
        {
            #region Private/internal fields

            private const int TrueVariationForBoolean = 0;
            private const int FalseVariationForBoolean = 1;

            internal readonly string _key;
            private int _offVariation;
            private bool _on;
            private int _fallthroughVariation;
            private List<LdValue> _variations;
            private Dictionary<string, Dictionary<int, HashSet<string>>> _targets;
            internal List<FlagRuleBuilder> _rules = null; // accessed by FlagRuleBuilder
            private long? _samplingRatio;
            private bool _excludeFromSummaries = false;
            private FlagMigrationBuilder _migrationBuilder = null;

            #endregion

            #region Internal constructors

            internal FlagBuilder(string key)
            {
                _key = key;
                _on = true;
                _variations = new List<LdValue>();
                _targets = new Dictionary<string, Dictionary<int, HashSet<string>>>();
                _rules = new List<FlagRuleBuilder>();
            }

            internal FlagBuilder(FlagBuilder from)
            {
                _key = from._key;
                _offVariation = from._offVariation;
                _on = from._on;
                _fallthroughVariation = from._fallthroughVariation;
                _variations = new List<LdValue>(from._variations);
                _targets = from._targets.ToDictionary( // deep-clone targets
                    kv => kv.Key,
                    kv => kv.Value.ToDictionary(kv1 => kv1.Key, kv1 => new HashSet<string>(kv1.Value)));
                _rules = new List<FlagRuleBuilder>(from._rules);
            }

            #endregion

            #region Public methods

            /// <summary>
            /// A shortcut for setting the flag to use the standard boolean configuration.
            /// </summary>
            /// <remarks>
            /// This is the default for all new flags created with <see cref="TestData.Flag(string)"/>.
            /// The flag will have two variations, <c>true</c> and <c>false</c> (in that order); it will
            /// return <c>false</c> whenever targeting is off, and <c>true</c> when targeting is on if
            /// no other settings specify otherwise.
            /// </remarks>
            /// <returns>the builder</returns>
            public FlagBuilder BooleanFlag()
            {
                if (IsBooleanFlag)
                {
                    return this;
                }
                return Variations(LdValue.Of(true), LdValue.Of(false))
                    .FallthroughVariation(TrueVariationForBoolean)
                    .OffVariation(FalseVariationForBoolean);
            }

            /// <summary>
            /// Sets targeting to be on or off for this flag.
            /// </summary>
            /// <remarks>
            /// The effect of this depends on the rest of the flag configuration, just as it does on the
            /// real LaunchDarkly dashboard. In the default configuration that you get from calling
            /// <see cref="TestData.Flag(string)"/> with a new flag key, the flag will return <c>false</c>
            /// whenever targeting is off, and <c>true</c> when targeting is on.
            /// </remarks>
            /// <param name="on">true if targeting should be on</param>
            /// <returns>the builder</returns>
            public FlagBuilder On(bool on)
            {
                _on = on;
                return this;
            }

            /// <summary>
            /// Specifies the fallthrough variation for a boolean flag.
            /// </summary>
            /// <remarks>
            /// <para>
            /// The fallthrough is the value that is returned if targeting is on and the context was not
            /// matched by a more specific target or rule.
            /// </para>
            /// <para>
            /// If the flag was previously configured with other variations, this also changes it to a
            /// boolean flag.
            /// </para>
            /// </remarks>
            /// <param name="variation">true if the flag should return true by default when targeting is on</param>
            /// <returns>the builder</returns>
            public FlagBuilder FallthroughVariation(bool variation)
            {
                return BooleanFlag().FallthroughVariation(VariationForBoolean(variation));
            }

            /// <summary>
            /// Specifies the index of the fallthrough variation.
            /// </summary>
            /// <remarks>
            /// The fallthrough is the value that is returned if targeting is on and the context was not
            /// matched by a more specific target or rule.
            /// </remarks>
            /// <param name="variationIndex">the desired fallthrough variation: 0 for the first, 1 for the second, etc.</param>
            /// <returns>the builder</returns>
            public FlagBuilder FallthroughVariation(int variationIndex)
            {
                _fallthroughVariation = variationIndex;
                return this;
            }

            /// <summary>
            /// Specifies the off variation for a boolean flag.
            /// </summary>
            /// <remarks>
            /// This is the variation that is returned whenever targeting is off.
            /// </remarks>
            /// <param name="variation">true if the flag should return true when targeting is off</param>
            /// <returns>the builder</returns>
            public FlagBuilder OffVariation(bool variation)
            {
                return BooleanFlag().OffVariation(VariationForBoolean(variation));
            }

            /// <summary>
            /// Specifies the index of the off variation.
            /// </summary>
            /// <remarks>
            /// This is the variation that is returned whenever targeting is off.
            /// </remarks>
            /// <param name="variationIndex">the desired off variation: 0 for the first, 1 for the second, etc.</param>
            /// <returns>the builder</returns>
            public FlagBuilder OffVariation(int variationIndex)
            {
                _offVariation = variationIndex;
                return this;
            }

            /// <summary>
            /// Sets the flag to always return the specified boolean variation for all contexts.
            /// </summary>
            /// <remarks>
            /// Targeting is switched on, any existing targets or rules are removed, and the flag's variations are
            /// set to <c>true</c> and <c>false</c>. The fallthrough variation is set to the specified value. The
            /// off variation is left unchanged.
            /// </remarks>
            /// <param name="variation">the desired true/false variation to be returned</param>
            /// <returns>the builder</returns>
            /// <seealso cref="VariationForAll(int)"/>
            /// <seealso cref="ValueForAll(LdValue)"/>
            public FlagBuilder VariationForAll(bool variation) =>
                BooleanFlag().VariationForAll(VariationForBoolean(variation));

            /// <summary>
            /// Sets the flag to always return the specified variation for all contexts.
            /// </summary>
            /// <remarks>
            /// The variation is specified by number, out of whatever variation values have already been
            /// defined. Targeting is switched on, and any existing targets or rules are removed. The fallthrough
            /// variation is set to the specified value. The off variation is left unchanged.
            /// </remarks>
            /// <param name="variationIndex">the desired variation: 0 for the first, 1 for the second, etc.</param>
            /// <returns>the builder</returns>
            /// <seealso cref="VariationForAll(bool)"/>
            /// <seealso cref="ValueForAll(LdValue)"/>
            public FlagBuilder VariationForAll(int variationIndex) =>
                On(true).ClearRules().ClearTargets().FallthroughVariation(variationIndex);

            /// <summary>
            /// Sets the flag to always return the specified variation value for all contexts.
            /// </summary>
            /// <remarks>
            /// The value may be of any JSON type, as defined by <see cref="LdValue"/>. This method changes the
            /// flag to have only a single variation, which is this value, and to return the same variation
            /// regardless of whether targeting is on or off. Any existing targets or rules are removed.
            /// </remarks>
            /// <param name="value">the desired value to be returned</param>
            /// <returns>the builder</returns>
            /// <seealso cref="VariationForAll(bool)"/>
            /// <seealso cref="VariationForAll(int)"/>
            public FlagBuilder ValueForAll(LdValue value)
            {
                _variations.Clear();
                _variations.Add(value);
                return VariationForAll(0);
            }

            /// <summary>
            /// Sets the flag to return the specified boolean variation for a specific user key (that is,
            /// for a context with that key whose context kind is "user") when targeting is on.
            /// </summary>
            /// <remarks>
            /// <para>
            /// This does not affect the flag's off variation that is used when targeting is off.
            /// </para>
            /// <para>
            /// If the flag was not already a boolean flag, this also changes it to a boolean flag.
            /// </para>
            /// </remarks>
            /// <param name="userKey">a user key</param>
            /// <param name="variation">the desired true/false variation to be returned for this user when
            /// targeting is on</param>
            /// <returns>the builder</returns>
            /// <seealso cref="VariationForUser(string, int)"/>
            public FlagBuilder VariationForUser(string userKey, bool variation) =>
                BooleanFlag().VariationForKey(ContextKind.Default, userKey, VariationForBoolean(variation));

            /// <summary>
            /// Sets the flag to return the specified boolean variation for a specific context, identified
            /// by context kind and key, when targeting is on.
            /// </summary>
            /// <remarks>
            /// <para>
            /// This does not affect the flag's off variation that is used when targeting is off.
            /// </para>
            /// <para>
            /// If the flag was not already a boolean flag, this also changes it to a boolean flag.
            /// </para>
            /// </remarks>
            /// <param name="contextKind">the context kind</param>
            /// <param name="key">the context key</param>
            /// <param name="variation">the desired true/false variation to be returned for this context when
            /// targeting is on</param>
            /// <returns>the builder</returns>
            /// <seealso cref="VariationForKey(ContextKind, string, int)"/>
            /// <seealso cref="VariationForUser(string, int)"/>
            public FlagBuilder VariationForKey(ContextKind contextKind, string key, bool variation) =>
                BooleanFlag().VariationForKey(contextKind, key, VariationForBoolean(variation));

            /// <summary>
            /// Sets the flag to return the specified variation for a specific user key (that is,
            /// for a context with that key whose context kind is "user") when targeting is on.
            /// </summary>
            /// <remarks>
            /// This has no effect when targeting is turned off for the flag. The variation is specified
            /// by number, out of whatever variation values have already been defined.
            /// </remarks>
            /// <param name="userKey">a user key</param>
            /// <param name="variationIndex">the desired variation to be returned for this user when
            /// targeting is on: 0 for the first, 1 for the second, etc.</param>
            /// <returns>the builder</returns>
            /// <seealso cref="VariationForUser(string, bool)"/>
            public FlagBuilder VariationForUser(string userKey, int variationIndex) =>
                VariationForKey(ContextKind.Default, userKey, variationIndex);

            /// <summary>
            /// Sets the flag to return the specified variation for a specific context, identified
            /// by context kind and key, when targeting is on.
            /// </summary>
            /// <remarks>
            /// This has no effect when targeting is turned off for the flag. The variation is specified
            /// by number, out of whatever variation values have already been defined.
            /// </remarks>
            /// <param name="contextKind">the context kind</param>
            /// <param name="key">the context key</param>
            /// <param name="variationIndex">the desired variation to be returned for this context when
            /// targeting is on: 0 for the first, 1 for the second, etc.</param>
            /// <returns>the builder</returns>
            /// <seealso cref="VariationForKey(ContextKind, string, bool)"/>
            /// <seealso cref="VariationForUser(string, int)"/>
            public FlagBuilder VariationForKey(ContextKind contextKind, string key, int variationIndex)
            {
                if (!_targets.TryGetValue(contextKind.Value, out var keysByVariation))
                {
                    keysByVariation = new Dictionary<int, HashSet<string>>();
                    _targets[contextKind.Value] = keysByVariation;
                }
                if (!keysByVariation.TryGetValue(variationIndex, out var keys))
                {
                    keys = new HashSet<string>();
                    keysByVariation[variationIndex] = keys;
                }
                keys.Add(key);
                foreach (var varAndKeys in keysByVariation)
                {
                    if (varAndKeys.Key != variationIndex)
                    {
                        varAndKeys.Value.Remove(key);
                    }
                }
                return this;
            }

            /// <summary>
            /// Changes the allowable variation values for the flag.
            /// </summary>
            /// <remarks>
            /// The value may be of any JSON type, as defined by <see cref="LdValue"/>. For instance, a boolean flag
            /// normally has <c>LdValue.Of(true), LdValue.Of(false)</c>; a string-valued flag might have
            /// <c>LdValue.Of("red"), LdValue.Of("green")</c>; etc.
            /// </remarks>
            /// <param name="values">the desired variations</param>
            /// <returns>the builder</returns>
            public FlagBuilder Variations(params LdValue[] values)
            {
                _variations.Clear();
                _variations.AddRange(values);
                return this;
            }

            /// <summary>
            /// Starts defining a flag rule, using the "is one of" operator. This matching expression
            /// only applies to contexts of a specific kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name attribute for the
            /// "company" context is "Ella" or "Monsoon":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfMatchContext("company", "name", LdValue.Of("Ella"), LdValue.Of("Monsoon"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="contextKind">the context kind to match</param>
            /// <param name="attribute">the attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>a <see cref="FlagRuleBuilder"/>; call <see cref="FlagRuleBuilder.ThenReturn(bool)"/>
            /// or <see cref="FlagRuleBuilder.ThenReturn(int)"/> to finish the rule, or add more tests with
            /// another method like <see cref="FlagRuleBuilder.AndMatchContext(ContextKind, string, LdValue[])"/></returns>
            public FlagRuleBuilder IfMatchContext(ContextKind contextKind, string attribute, params LdValue[] values) =>
                new FlagRuleBuilder(this).AndMatchContext(contextKind, attribute, values);

            /// <summary>
            /// Starts defining a flag rule, using the "is one of" operator. This is a shortcut for calling
            /// <see cref="IfMatchContext(ContextKind, string, LdValue[])"/> with "user" as the context kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name is "Patsy" or "Edina":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfMatch("name", LdValue.Of("Patsy"), LdValue.Of("Edina"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="attribute">the user attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>a <see cref="FlagRuleBuilder"/>; call <see cref="FlagRuleBuilder.ThenReturn(bool)"/>
            /// or <see cref="FlagRuleBuilder.ThenReturn(int)"/> to finish the rule, or add more tests with
            /// another method like <see cref="FlagRuleBuilder.AndMatch(string, LdValue[])"/></returns>
            public FlagRuleBuilder IfMatch(string attribute, params LdValue[] values) =>
                new FlagRuleBuilder(this).AndMatch(attribute, values);

            /// <summary>
            /// Starts defining a flag rule, using the "is not one of" operator. This matching expression
            /// only applies to contexts of a specific kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name attribute for the
            /// "company" context is neither "Pendant" nor "Sterling Cooper":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfNotMatchContext("company", "name", LdValue.Of("Pendant"), LdValue.Of("Sterling Cooper"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="contextKind">the context kind to match</param>
            /// <param name="attribute">the attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>a <see cref="FlagRuleBuilder"/>; call <see cref="FlagRuleBuilder.ThenReturn(bool)"/>
            /// or <see cref="FlagRuleBuilder.ThenReturn(int)"/> to finish the rule, or add more tests with
            /// another method like <see cref="FlagRuleBuilder.AndMatchContext(ContextKind, string, LdValue[])"/></returns>
            public FlagRuleBuilder IfNotMatchContext(ContextKind contextKind, string attribute, params LdValue[] values) =>
                new FlagRuleBuilder(this).AndNotMatchContext(contextKind, attribute, values);

            /// <summary>
            /// Starts defining a flag rule, using the "is not one of" operator. This is a shortcut for calling
            /// <see cref="IfNotMatchContext(ContextKind, string, LdValue[])"/> with "user" as the context kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name is neither
            /// "Saffron" nor "Bubble":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfNotMatch("name", LdValue.Of("Saffron"), LdValue.Of("Bubble"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="attribute">the user attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>a <see cref="FlagRuleBuilder"/>; call <see cref="FlagRuleBuilder.ThenReturn(bool)"/>
            /// or <see cref="FlagRuleBuilder.ThenReturn(int)"/> to finish the rule, or add more tests with
            /// another method like <see cref="FlagRuleBuilder.AndMatch(string, LdValue[])"/></returns>
            public FlagRuleBuilder IfNotMatch(string attribute, params LdValue[] values) =>
                new FlagRuleBuilder(this).AndNotMatch(attribute, values);

            /// <summary>
            /// Removes any existing rules from the flag.
            /// </summary>
            /// <remarks>
            /// This undoes the effect of methods like <see cref="IfMatch(string, LdValue[])"/>.
            /// </remarks>
            /// <returns>the builder</returns>
            public FlagBuilder ClearRules()
            {
                _rules.Clear();
                return this;
            }

            /// <summary>
            /// Removes any existing user/context targets from the flag.
            /// </summary>
            /// <remarks>
            /// This undoes the effect of methods like <see cref="VariationForUser(string, bool)"/>.
            /// </remarks>
            /// <returns>the builder</returns>
            public FlagBuilder ClearTargets()
            {
                _targets.Clear();
                return this;
            }

            /// <summary>
            /// Sets the sampling ratio for the flag.
            /// </summary>
            /// <param name="samplingRatio">the new sampling ratio</param>
            /// <returns>the builder</returns>
            public FlagBuilder SamplingRatio(long samplingRatio)
            {
                _samplingRatio = samplingRatio;
                return this;
            }

            /// <summary>
            /// Sets if the flag should be excluded from summary events.
            /// </summary>
            /// <param name="excludeFromSummaries">true to exclude the flag from summaries</param>
            /// <returns>the builder</returns>
            public FlagBuilder ExcludeFromSummaries(bool excludeFromSummaries)
            {
                _excludeFromSummaries = excludeFromSummaries;
                return this;
            }

            /// <summary>
            /// Used to configure migration settings for the flag.
            /// </summary>
            /// <returns>the builder</returns>
            public FlagBuilder Migration(FlagMigrationBuilder migrationBuilder)
            {
                _migrationBuilder = migrationBuilder;
                return this;
            }

            #endregion

            #region Internal methods

            internal ItemDescriptor CreateFlag(int version)
            {

                var userTargets = _targets.Where(kindAndMap => kindAndMap.Key == ContextKind.Default.Value)
                    .SelectMany(kindAndMap => kindAndMap.Value)
                    .Where(varAndKeys => varAndKeys.Value.Any())
                    .OrderBy(varAndKeys => varAndKeys.Key) // order by variation for test determinacy
                    .Select(varAndKeys => new Target(
                        null,
                        varAndKeys.Value.OrderBy(key => key), // order by key for test determinacy
                        varAndKeys.Key))
                    .ToImmutableList();

                var contextTargets = _targets.OrderBy(kindAndMap => kindAndMap.Key) // order by kind for test determinacy
                    .SelectMany(kindAndMap => kindAndMap.Value.OrderBy(varAndKeys => varAndKeys.Key) // order by variation for test determinacy
                        .Where(varAndKeys => varAndKeys.Value.Any())
                        .Select(varAndKeys => new Target(
                            ContextKind.Of(kindAndMap.Key),
                            // per data model rules, we don't include keys here for the "user" kind
                            kindAndMap.Key == ContextKind.Default.Value ? Enumerable.Empty<string>() :
                                varAndKeys.Value.OrderBy(key => key), // order by key for test determinacy
                            varAndKeys.Key)))
                    .ToImmutableList();

                var rules = _rules.Select((r, index) => new FlagRule(
                    r._variation,
                    null,
                    $"rule{index}",
                    r._clauses.Select(c => new Internal.Model.Clause(
                        null,
                        c._attribute,
                        Operator.ForName(c._operator),
                        c._values.ToArray(),
                        c._negate
                        )).ToList(),
                    false))
                    .ToImmutableList();

                Migration? migration = null;
                if (_migrationBuilder != null)
                {
                    migration = new Migration(_migrationBuilder._checkRatio);
                }

                var flag = new FeatureFlag(
                    _key,
                    version,
                    false,
                    _on,
                    ImmutableList.Create<Prerequisite>(),
                    userTargets,
                    contextTargets,
                    rules,
                    new VariationOrRollout(_fallthroughVariation, null),
                    _offVariation,
                    _variations,
                    "",
                    false,
                    false,
                    null,
                    false,
                    _samplingRatio,
                    _excludeFromSummaries,
                    migration
                    );
                return new ItemDescriptor(version, flag);
            }

            internal bool IsBooleanFlag =>
                _variations.Count == 2 &&
                    _variations[TrueVariationForBoolean] == LdValue.Of(true) &&
                    _variations[FalseVariationForBoolean] == LdValue.Of(false);

            internal static int VariationForBoolean(bool value) =>
                value ? TrueVariationForBoolean : FalseVariationForBoolean;

            #endregion
        }

        /// <summary>
        /// A builder for feature flag rules to be used with <see cref="FlagBuilder"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the LaunchDarkly model, a flag can have any number of rules, and a rule can have any number of
        /// clauses. A clause is an individual test such as "name is 'X'". A rule matches a context if all of the
        /// rule's clauses match the context.
        /// </para>
        /// <para>
        /// To start defining a rule, use one of the flag builder's matching methods such as
        /// <see cref="FlagBuilder.IfMatchContext(ContextKind, string, LdValue[])"/>. This defines the first clause for
        /// the rule. Optionally, you may add more clauses with the rule builder's methods such as
        /// <see cref="AndMatchContext(ContextKind, string, LdValue[])"/>. Finally, call <see cref="ThenReturn(bool)"/> or
        /// <see cref="ThenReturn(int)"/> to finish defining the rule.
        /// </para>
        /// </remarks>
        public sealed class FlagRuleBuilder
        {
            private readonly FlagBuilder _parent;
            internal readonly List<Clause> _clauses = new List<Clause>();
            internal int _variation;

            internal FlagRuleBuilder(FlagBuilder parent)
            {
                _parent = parent;
            }

            /// <summary>
            /// Adds another clause, using the "is one of" operator. This matching expression
            /// only applies to contexts of a specific kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name attribute for the
            /// "company" context is "Ella" and the country is "gb":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfMatchContext("company", "name", LdValue.Of("Ella"))
            ///         .AndMatchContext("company", "country", LdValue.Of("gb"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="contextKind">the context kind to match</param>
            /// <param name="attribute">the attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>the rule builder</returns>
            public FlagRuleBuilder AndMatchContext(ContextKind contextKind, string attribute, params LdValue[] values) =>
                AddClause(contextKind, AttributeRef.FromPath(attribute), "in", values, false);

            /// <summary>
            /// Adds another clause, using the "is one of" operator. This is a shortcut for calling
            /// <see cref="AndMatchContext(ContextKind, string, LdValue[])"/> with "user" as the context kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name is "Patsy" and the
            /// country is "gb":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfMatch("name", LdValue.Of("Patsy"))
            ///         .AndMatch("country", LdValue.Of("gb"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="attribute">the user attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>the rule builder</returns>
            public FlagRuleBuilder AndMatch(string attribute, params LdValue[] values) =>
                AndMatchContext(ContextKind.Default, attribute, values);

            /// <summary>
            /// Adds another clause, using the "is not one of" operator. This matching expression
            /// only applies to contexts of a specific kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name attribute for the
            /// "company" context is "Ella" and the country is not "gb":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfMatchContext("company", "name", LdValue.Of("Ella"))
            ///         .AndNotMatchContext("company", "country", LdValue.Of("gb"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="contextKind">the context kind to match</param>
            /// <param name="attribute">the attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>the rule builder</returns>
            public FlagRuleBuilder AndNotMatchContext(ContextKind contextKind, string attribute, params LdValue[] values) =>
                AddClause(contextKind, AttributeRef.FromPath(attribute), "in", values, true);

            /// <summary>
            /// Adds another clause, using the "is not one of" operator. This is a shortcut for calling
            /// <see cref="AndNotMatchContext(ContextKind, string, LdValue[])"/> with "user" as the context kind.
            /// </summary>
            /// <remarks>
            /// <para>
            /// For example, this creates a rule that returns <c>true</c> if the name is "Patsy" and the
            /// country is not "gb":
            /// </para>
            /// <example>
            /// <code>
            ///     testData.Update(testData.Flag("flag-key")
            ///         .IfMatch("name", LdValue.Of("Patsy"))
            ///         .AndNotMatch("country", LdValue.Of("gb"))
            ///         .ThenReturn(true));
            /// </code>
            /// </example>
            /// </remarks>
            /// <param name="attribute">the user attribute to match against</param>
            /// <param name="values">values to compare to</param>
            /// <returns>the rule builder</returns>
            public FlagRuleBuilder AndNotMatch(string attribute, params LdValue[] values) =>
                AndNotMatchContext(ContextKind.Default, attribute, values);

            private FlagRuleBuilder AddClause(ContextKind contextKind, AttributeRef attr, string op, LdValue[] values, bool negate)
            {
                _clauses.Add(new Clause(attr, op, values, negate));
                return this;
            }

            /// <summary>
            /// Finishes defining the rule, specifying the result value as a boolean.
            /// </summary>
            /// <param name="variation">the value to return if the rule matches the user</param>
            /// <returns></returns>
            public FlagBuilder ThenReturn(bool variation)
            {
                _parent.BooleanFlag();
                return ThenReturn(FlagBuilder.VariationForBoolean(variation));
            }

            /// <summary>
            /// Finishes defining the rule, specifying the result as a variation index.
            /// </summary>
            /// <param name="variationIndex">the variation to return if the rule matches the user: 0 for the first, 1
            /// for the second, etc.</param>
            /// <returns>the flag builder</returns>
            public FlagBuilder ThenReturn(int variationIndex)
            {
                _variation = variationIndex;
                if (_parent._rules == null)
                {
                    _parent._rules = new List<FlagRuleBuilder>();
                }
                _parent._rules.Add(this);
                return _parent;
            }
        }

        /// <summary>
        /// Used to build the migration settings for a feature flag.
        /// </summary>
        public sealed class FlagMigrationBuilder
        {
            internal long? _checkRatio;

            /// <summary>
            /// Set the check ratio for the migration.
            /// </summary>
            /// <remarks>
            /// The check ratio is used in determining when to check the consistency of a migration.
            /// </remarks>
            /// <param name="checkRatio">the desired check ratio</param>
            /// <returns>the builder</returns>
            public FlagMigrationBuilder CheckRatio(long? checkRatio)
            {
                _checkRatio = checkRatio;
                return this;
            }
        }

        #endregion

        #region Internal inner type

        internal class Clause
        {
            internal readonly AttributeRef _attribute;
            internal readonly string _operator;
            internal readonly LdValue[] _values;
            internal readonly bool _negate;

            internal Clause(AttributeRef attribute, string op, LdValue[] values, bool negate)
            {
                _attribute = attribute;
                _operator = op;
                _values = values;
                _negate = negate;
            }
        }

        internal class DataSourceImpl : IDataSource
        {
            private readonly TestData _parent;
            private readonly IDataSourceUpdates _updates;
            private readonly Logger _log;

            internal DataSourceImpl(TestData parent, IDataSourceUpdates updates, Logger log)
            {
                _parent = parent;
                _updates = updates;
                _log = log;
            }

            public Task<bool> Start()
            {
                _updates.Init(_parent.MakeInitData());
                _updates.UpdateStatus(DataSourceState.Valid, null);
                return Task.FromResult(true);
            }

            public bool Initialized => true;

            public void Dispose()
            {
                _parent.ClosedInstance(this);
            }

            internal void DoInit(FullDataSet<ItemDescriptor> data)
            {
                _log.Debug("using initial test data:\n{0}",
                    LogValues.Defer(() =>
                        string.Join("\n", data.Data.Select(coll =>
                            coll.Key.Name + ":\n" + string.Join("\n", coll.Value.Items.Select(kv =>
                                coll.Key.Serialize(kv.Value)
                            ))
                        ))
                    ));
                _updates.Init(data);
            }

            internal void DoUpdate(DataKind kind, string key, ItemDescriptor item)
            {
                _log.Debug("updating \"{0}\" in {1} to {2}", key, kind.Name, LogValues.Defer(() =>
                    kind.Serialize(item)));
                _updates.Upsert(kind, key, item);
            }

            internal void DoUpdateStatus(DataSourceState newState, DataSourceStatus.ErrorInfo? newError)
            {
                _log.Debug("updating status to {0}{1}", newState,
                    newError.HasValue ? (" (" + newError.Value + ")") : "");
                _updates.UpdateStatus(newState, newError);
            }
        }

        #endregion
    }
}
