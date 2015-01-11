using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LaunchDarkly.Tests
{
    class AnyCustomData
    {
        public int Id { get; set; }
        public string SomeString { get; set; }
        public Boolean SomeBool { get; set; }

        public AnyCustomData(int id, string someString, Boolean someBool)
        {
            Id = id;
            SomeString = someString;
            SomeBool = someBool;
        }
    }
}
