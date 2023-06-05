using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slik.Projects
{
    [Serializable]
    class FunctionTable
    {
        public List<Function> Functions = new();
        public string Name;

        public FunctionTable(string name)
        {
            Name = name;
        }
    }

    [Serializable]
    class SlikClass
    {
        public FunctionTable FunctionTable = new("Main");

        public string Name;
        public string Description;
        public DateTime Created;

        public SlikClass(string name, string description = "")
        {
            Name = name;
            Description = description;
            Created = DateTime.Now;
        }


        // For serialization
        private SlikClass()
        {
        }
    }
}
