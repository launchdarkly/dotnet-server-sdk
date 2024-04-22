using System;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    // This is the standard implementation of BigSegmentStoreStatusProvider. Most of the work is done by
    // BigSegmentStoreWrapper, which exposes the methods that other SDK components need to access the store.
    //
    // We always create this component regardless of whether there really is a store. If there is no store (so
    // there is no BigSegmentStoreWrapper) then we won't actually be doing any Big Segments stuff, or sending
    // any status updates, but this API object still exists so your app won't crash if you try to use
    // Status or StatusChanged.
    internal sealed class BigSegmentStoreStatusProviderImpl : IBigSegmentStoreStatusProvider
    {
        private readonly BigSegmentStoreWrapper _storeWrapper;

        public BigSegmentStoreStatus Status =>
            _storeWrapper is null ? new BigSegmentStoreStatus { Available = false } :
            _storeWrapper.GetStatus();

        public event EventHandler<BigSegmentStoreStatus> StatusChanged
        {
            add
            {
                if (_storeWrapper != null)
                {
                    _storeWrapper.StatusChanged += value;
                }
            }
            remove
            {
                if (_storeWrapper != null)
                {
                    _storeWrapper.StatusChanged -= value;
                }
            }
        }

        internal BigSegmentStoreStatusProviderImpl(BigSegmentStoreWrapper storeWrapper)
        {
            _storeWrapper = storeWrapper;
        }
    }
}
