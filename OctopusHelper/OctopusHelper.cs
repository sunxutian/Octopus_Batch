namespace OctopusVariableImport.OctopusHelper
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Octopus.Client;
    using Octopus.Client.Exceptions;
    using Octopus.Client.Model;
    using OctopusVariableImport.Model;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class OctopusHelper
    {
        private const string _configJsonName = "AppConfig.json";

        private OctopusRepository _repo;

        private VariableSetResource _variableSet;

        private ProjectResource _project;

        private List<ReferenceDataItem> _environmentList;

        private string _scopeId;

        private string _environment;

        private bool _isTargetFileJson;

        private string _path;

        public OctopusRepository Repo
        {
            get
            {
                if (this._repo == null || this._project == null)
                {
                    this._repo = this.SetupOctopusRepository();
                }

                return this._repo;
            }
        }

        private VariableSetResource VariableSet
        {
            get
            {
                if (this._variableSet == null)
                {
                    this._variableSet = this.GetVariableSet();
                }

                return this._variableSet;
            }
        }

        public string GetEnvironmentFromId(string id) => this._environmentList.Find(r => r.Id.Equals(id)).Name;

        private JObject ConfigJson { get; set; }

        private VariableSetResource GetVariableSet() => this._repo.VariableSets.Get(this._project.Link("Variables"));

        private void ResetVariableSet()
        {
            this._variableSet = this.GetVariableSet();
        }

        public OctopusHelper()
        {
            string configJsonPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), _configJsonName);
            using (TextReader textReader = new StreamReader(configJsonPath))
            {
                string json = textReader.ReadToEnd();
                this.ConfigJson = JObject.Parse(json);
                this.SetupOctopusRepository();
            }
        }

        #region Setup Connection

        private OctopusRepository SetupOctopusRepository()
        {
            string octopusServerUrl = this.ConfigJson["serverUrl"].Value<string>();
            string octopusServerApiKey = this.ConfigJson["apiKey"].Value<string>();
            string octopusServerUserName = this.ConfigJson["userName"].Value<string>();
            string octopusServerPassword = this.ConfigJson["password"].Value<string>();
            string octopusServerProjectName = this.ConfigJson["projectName"].Value<string>();
            string fileFormat = this.ConfigJson["variablesFileFormat"].Value<string>();
            this._path = this.ConfigJson["variablesFilePath"].Value<string>();

            if (string.IsNullOrEmpty(octopusServerUrl))
            {
                throw new Exception("AppConfig.json has to have a octopusServer Url");
            }

            this._repo = string.IsNullOrEmpty(octopusServerApiKey)
                             ? new OctopusRepository(new OctopusServerEndpoint(octopusServerUrl))
                             : new OctopusRepository(new OctopusServerEndpoint(octopusServerUrl, octopusServerApiKey));

            if (!string.IsNullOrEmpty(octopusServerUserName) && !string.IsNullOrEmpty(octopusServerPassword))
            {
                this._repo.Users.SignIn(
                    new LoginCommand() { Username = octopusServerUserName, Password = octopusServerPassword });
            }

            this._project = this._repo.Projects.FindByName(octopusServerProjectName);
            if (this._project == null)
            {
                Console.WriteLine("Project not valid");
                throw new ArgumentException("Project not valid");
            }
            // get scope
            this._environment = this.ConfigJson["environment"].Value<string>();
            //var variableSet = repo.VariableSets.Get(this._project.Link("Variables"));
            this._environmentList = this.VariableSet.ScopeValues.Environments;
            var scopeRef = this._environmentList.FirstOrDefault(e => e.Name.Equals(this._environment));
            if (scopeRef == null)
            {
                Console.WriteLine("environment not valid");
                throw new ArgumentException("environment not valid");
            }
            this._scopeId = scopeRef.Id;

            this._isTargetFileJson = !string.IsNullOrEmpty(fileFormat)
                                     && fileFormat.Equals("json", StringComparison.CurrentCultureIgnoreCase);
            return this._repo;
        }

        #endregion Setup Connection

        #region Get Variables From Octopus

        public List<VariableResource> GetAllVariables()
        {
            try
            {
                // Get the variables for editing
                var variableSet = this.GetVariableSet();
                var variables = variableSet.Variables.ToList();
                return variables;
            }
            catch (OctopusSecurityException securityException)
            {
                Console.WriteLine($"Permission issue with Octopus\r\n{securityException.Message}");
                throw;
            }
        }

        public string WriteOctopusVariablesToJson(List<VariableResource> variables)
        {
            string json = string.Empty;
            if (variables == null || !variables.Any())
            {
                return string.Empty;
            }
            string outputPath = ConfigJson["octopusVariableExportPath"].Value<string>();
            if (outputPath != null)
            {
                using (var stream = File.OpenWrite(outputPath))
                using (var writer = new StreamWriter(stream))
                {
                    json = JsonConvert.SerializeObject(variables, Formatting.Indented, new OctopusVariableJsonConverter(ScopeField.Environment, this._environmentList));
                    writer.Write(json);
                }
            }

            return json;
        }

        #endregion Get Variables From Octopus

        #region Add Octopus Variables

        private IList<VariableResource> AddVariableToResource(
            OctopusVariableClientModel variableModel,
            params KeyValuePair<ScopeField, string>[] scopes)
        {
            VariableResource variable = new VariableResource()
            {
                Name = variableModel.Name,
                Value = variableModel.Value,
                IsEditable = variableModel.IsEditable,
                IsSensitive = variableModel.IsSensitive,
                Scope = new ScopeSpecification()
            };
            foreach (KeyValuePair<ScopeField, string> scope in scopes)
            {
                variable.Scope.Add(scope.Key, scope.Value);
            }

            this.VariableSet.Variables.Add(variable);

            return this.VariableSet.Variables;
        }

        private void AddVariable(
            OctopusVariableClientModel variableModel,
            ScopeField scopeField = ScopeField.Environment)
        {
            try
            {
                // Test if variable exists
                VariableResource variable =
                    this.VariableSet.Variables.FirstOrDefault(
                        v =>
                        v.Name.Equals(variableModel.Name) && v.Scope.ContainsKey(scopeField)
                        && v.Scope[scopeField].Contains(this._scopeId));

                // test if we already have the same key/value on different environment
                VariableResource variableOnDifferentEnv =
                    this.VariableSet.Variables.FirstOrDefault(
                        v =>
                        v.Name.Equals(variableModel.Name) && !v.IsSensitive && v.Scope.ContainsKey(scopeField) && v.Value != null
                        && v.Value.Equals(variableModel.Value) && !v.Scope[scopeField].Contains(this._scopeId));

                // variable exists
                if (variable != null)
                {
                    // if variable is sensitive, its value returns null from Octopus, always override
                    if (variable.IsSensitive)
                    {
                        variable.Value = variableModel.Value;
                        variable.IsEditable = variableModel.IsEditable;
                        return;
                    }
                    if (variable.Value.Equals(variableModel.Value))
                    {
                        variable.IsEditable = variableModel.IsEditable;
                        variable.IsSensitive = variableModel.IsSensitive;
                        return;
                    }

                    Console.WriteLine($"Modifying {variableModel.Name} : {variableModel.Value} on {this._environment}");

                    // if key/value exists already on different environment. add current env to that variable and delete old variable
                    if (variableOnDifferentEnv != null && variableOnDifferentEnv.IsEditable)
                    {
                        variableOnDifferentEnv.Scope[scopeField].Add(this._scopeId);
                        this.VariableSet.Variables.Remove(variable);
                    }
                    else
                    {
                        // Modify the existing variable if only one env is tagged to this variable
                        if (variable.IsEditable && variable.Scope[scopeField].Count == 1)
                        {
                            variable.Value = variableModel.Value;
                        }
                        else if (variable.IsEditable)
                        {
                            // remove environment from old
                            variable.Scope[scopeField].Remove(this._scopeId);
                            // add to new
                            this.AddVariableToResource(
                                variableModel,
                                new KeyValuePair<ScopeField, string>(scopeField, this._scopeId));
                        }
                    }
                }
                // variable not exists
                else
                {
                    Console.WriteLine($"Adding {variableModel.Name} : {variableModel.Value} on {this._environment}");

                    // if exists same key/value variable on different env, add env to that variable
                    if (variableOnDifferentEnv != null && variableOnDifferentEnv.IsEditable)
                    {
                        variableOnDifferentEnv.Scope[scopeField].Add(this._scopeId);
                    }
                    else
                    {
                        // Add a new variable
                        this.AddVariableToResource(
                            variableModel,
                            new KeyValuePair<ScopeField, string>(scopeField, this._scopeId));
                    }
                }
            }
            catch (OctopusSecurityException securityException)
            {
                Console.WriteLine($"Permission issue with Octopus\r\n{securityException.Message}");
                throw;
            }
        }

        private void AddVariables(
            List<OctopusVariableClientModel> variables,
            ScopeField scopeField = ScopeField.Environment)
        {
            if (variables == null || !variables.Any())
            {
                return;
            }

            foreach (OctopusVariableClientModel variable in variables)
            {
                this.AddVariable(variable, scopeField);
            }
        }

        #endregion Add Octopus Variables

        #region Get Variables from Source

        private List<OctopusVariableClientModel> GetVariablesToBeAdded()
        {
            if (this._isTargetFileJson)
            {
                return this.GetVariablesToBeAddedFromJson();
            }
            else
            {
                return this.GetVariablesToBeAddedFromCsv();
            }
        }

        private List<OctopusVariableClientModel> GetVariablesToBeAddedFromCsv()
        {
            var extension = Path.GetExtension(this._path);

            if (string.IsNullOrEmpty(extension) || !extension.Equals(".csv", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new ArgumentException("csv file path error...");
            }
            try
            {
                List<OctopusVariableClientModel> variables = new List<OctopusVariableClientModel>();
                using (StreamReader reader = new StreamReader(File.OpenRead(this._path)))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] keyValuePair = line.Split(';');
                        OctopusVariableClientModel variable = new OctopusVariableClientModel()
                        {
                            Name = keyValuePair[0],
                            Value = keyValuePair[1]
                        };
                        if (keyValuePair.Length == 3)
                        {
                            variable.IsSensitive = bool.Parse(keyValuePair[2]);
                        }

                        if (keyValuePair.Length == 4)
                        {
                            variable.IsEditable = bool.Parse(keyValuePair[3]);
                        }
                        variables.Add(variable);
                    }
                }

                return variables;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reading CSV Error... {ex.Message}");
                throw;
            }
        }

        private List<OctopusVariableClientModel> GetVariablesToBeAddedFromJson()
        {
            var extension = Path.GetExtension(this._path);

            if (string.IsNullOrEmpty(extension) || !extension.Equals(".json", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new ArgumentException("json file path error...");
            }

            using (TextReader textReader = new StreamReader(File.OpenRead(this._path)))
            {
                string json = textReader.ReadToEnd();
                List<OctopusVariableClientModel> variables =
                    JsonConvert.DeserializeObject<List<OctopusVariableClientModel>>(json);

                return variables;
            }
        }

        #endregion Get Variables from Source

        public void BatchAddVariables()
        {
            var variables = this.GetVariablesToBeAdded();
            AddVariables(variables);
            // Save the variables
            this._repo.VariableSets.Modify(this.VariableSet);
            this.ResetVariableSet();
        }
    }

    public class OctopusVariableJsonConverter : JsonConverter
    {
        private ScopeField _scopeFiledToConvert;

        private List<ReferenceDataItem> _reference;

        public OctopusVariableJsonConverter(ScopeField scopeField, List<ReferenceDataItem> reference) : base()
        {
            _scopeFiledToConvert = scopeField;
            _reference = reference;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ScopeSpecification scopes = value as ScopeSpecification;
            if (scopes != null)
            {
                //writer.WritePropertyName("Scope");
                writer.WriteStartObject();
                foreach (KeyValuePair<ScopeField, ScopeValue> scope in scopes)
                {
                    writer.WritePropertyName(scope.Key.ToString());
                    writer.WriteStartArray();

                    foreach (string s in scope.Value)
                    {
                        if (scope.Key == this._scopeFiledToConvert)
                        {
                            string env = this._reference.Find(r => r.Id.Equals(s)).Name;
                            writer.WriteValue(env);
                        }
                        else
                        {
                            writer.WriteValue(s);
                        }
                    }

                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ScopeSpecification);
        }
    }
}