using System;

namespace LaunchDarkly.Client
{

    public interface ITypeConverter
    {

        object Convert(object value, Type type);

    }

}