using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctopusVariableImport.Model
{
    public class OctopusVariableClientModel
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public bool IsSensitive { get; set; }

        public bool IsEditable { get; set; }
    }
}
