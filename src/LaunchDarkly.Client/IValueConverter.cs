using System;

namespace LaunchDarkly.Client
{

    public interface IValueConverter
    {

        object Convert(object value, Type type);

    }

}