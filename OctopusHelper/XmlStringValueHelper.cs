namespace OctopusVariableImport.OctopusHelper
{
    using System;
    using System.ComponentModel;
    using System.Xml.Linq;

    public static class XmlStringValueHelper
    {
        public static T Value<T>(this XAttribute attribute) where T : IConvertible
        {
            return attribute.Value<T>(
                str =>
                    {
                        TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
                        if (converter.CanConvertFrom(typeof(string)))
                        {
                            return (T)converter.ConvertFromString(str);
                        }
                        return default(T);
                    });
        }

        public static T Value<T>(this XAttribute attribute, Converter<string,T> converter) where T : IConvertible
        {
            if (string.IsNullOrEmpty(attribute?.Value))
            {
                return default(T);
            }

            if (converter == null)
            {
                throw new ArgumentException();
            }

            return converter(attribute.Value);
        }
    }
}
