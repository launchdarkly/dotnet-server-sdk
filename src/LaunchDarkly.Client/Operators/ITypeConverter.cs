using System;

namespace LaunchDarkly.Client.CustomAttributes
{

    public interface ITypeConverter
    {

        object Convert(object value, Type type);

    }

}