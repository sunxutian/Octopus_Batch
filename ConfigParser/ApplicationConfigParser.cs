namespace OctopusVariableImport.ConfigParser
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using OctopusVariableImport.Model;
    using OctopusVariableImport.OctopusHelper;

    using Formatting = Newtonsoft.Json.Formatting;

    public static class ApplicationConfigParser
    {
        private const string _configJsonName = "AppConfig.json";

        private static JObject ConfigJson { get; set; }


        public static List<OctopusVariableClientModel> ParserConfig()
        {
            string configJsonPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), _configJsonName);
            using (TextReader textReader = new StreamReader(configJsonPath))
            {
                string json = textReader.ReadToEnd();
                ConfigJson = JObject.Parse(json);
            }
            return ParseConfigInternal();
        }

        private static List<OctopusVariableClientModel> ParseConfigInternal()
        {
            string xmlPath = ConfigJson["originalConfigPaths"].Value<string>();
            var extension = Path.GetExtension(xmlPath);

            if (string.IsNullOrEmpty(extension) || !extension.Equals(".config", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new ArgumentException("config file not valid");
            }

            XElement config = XElement.Load(xmlPath);

            if (config.Name.LocalName.Equals("Config", StringComparison.CurrentCultureIgnoreCase))
            {
                var allKeyValuePairs = config.Elements("add").Where(e => e.HasAttributes && e.Attribute("key") != null && e.Attribute("value") != null).ToList();
                JObject jObject = new JObject();
                List<OctopusVariableClientModel> variables = new List<OctopusVariableClientModel>();

                using (TextWriter textWriter = new StreamWriter(ConfigJson["variablesFilePath"].Value<string>()))
                using (JsonWriter writer = new JsonTextWriter(textWriter) {Formatting = Formatting.Indented})
                {
                    foreach (XElement keyValuePair in allKeyValuePairs)
                    {
                        var key = keyValuePair.Attribute("key").Value;
                        var value = keyValuePair.Attribute("value").Value;
                        var isSensitive = keyValuePair.Attribute("sensitive")?.Value<bool>() ?? false;
                        // default to be editable
                        var isEditable = keyValuePair.Attribute("editable")?.Value<bool>()?? true;
                        var variable = new OctopusVariableClientModel
                                           {
                                               Name = key,
                                               Value = value,
                                               IsSensitive = isSensitive,
                                               IsEditable = isEditable
                                           };
                        variables.Add(variable);
                        //var json = JsonConvert.SerializeObject(variable);
                        //jObject.Add(key, value);
                        //writer.WritePropertyName(key);
                        //writer.WriteRaw(json);
                    }
                    var json = JsonConvert.SerializeObject(variables, Formatting.Indented);
                    writer.WriteRaw(json);
                }

                return variables;
            }

            return null;
        }
    }
}
