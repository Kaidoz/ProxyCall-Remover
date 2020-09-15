using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyCall_Remover.Deobfuscation
{
    internal interface IDeobfuscator
    {
        string Name { get; }

        void RemoveProtection(ModuleDef module);

        int GetResult();

        void Dispose();
    }
}