using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctopusVariableImport
{
    using Octopus.Client.Model;

    using OctopusVariableImport.ConfigParser;

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                ShowHelp();
                return;
            }
            if (args.Contains("-p") || args.Contains("--parse"))
            {
                Console.WriteLine("Parsing config to json...");
                ApplicationConfigParser.ParserConfig();
                Console.WriteLine("Parsing config to json succeed...");
                return;
            }
            else
            {
                Console.WriteLine("-----Connecting to Octopus------");
                OctopusHelper.OctopusHelper helper = new OctopusHelper.OctopusHelper();

                if (args.Contains("-e") || args.Contains("--export"))
                {
                    helper.WriteOctopusVariablesToJson(helper.GetAllVariables());
                    return;
                }
                else if (args.Contains("-i") || args.Contains("--import"))
                {
                    Console.WriteLine("You are about to import variables to Octopus server\r\nDo you want to continue? Y/N");
                    var key = Console.ReadKey();
                    if (key.Key != ConsoleKey.Y)
                    {
                        return;
                    }
                    Console.WriteLine();
                    Console.WriteLine("Starting Reading Config...");
                    Console.WriteLine("Starting Importing Variables...");
                    helper.BatchAddVariables();
                    //PrintOutCurrentVariables(helper);
                    Console.WriteLine("Finish Importing Variables...");
                    Console.ReadKey();
                    return;
                }

            }

            ShowHelp();
        }

        private static void ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine("---Import Tool Of Octopus---");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("---Use AppConfig.json to config---");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("-p/--parse To parse config file to json");
            Console.WriteLine();
            Console.WriteLine("-e/--export To export variables on Octopus");
            Console.WriteLine();
            Console.WriteLine("-i/--import To import json variables to Octopus");
        }

        private static void PrintOutCurrentVariables(OctopusHelper.OctopusHelper helper)
        {
            var values = helper.GetAllVariables();
            foreach (VariableResource variable in values)
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<ScopeField, ScopeValue> valuePair in variable.Scope)
                {
                    string envs = string.Empty;
                    if (valuePair.Key == ScopeField.Environment)
                    {
                        foreach (ScopeValue scopeValue in valuePair.Value)
                        {
                            envs += helper.GetEnvironmentFromId(scopeValue.ToString());
                        }
                    }
                    else
                    {
                        envs = valuePair.Value.ToString();
                    }
                    sb.Append($"{valuePair.Key} : {envs}");
                }
                Console.WriteLine($"Key:{variable.Name} - Value:{variable.Value} --- ({sb.ToString()})");
            }
        }
    }
}
