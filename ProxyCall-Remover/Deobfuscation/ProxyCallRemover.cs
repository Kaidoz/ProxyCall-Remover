using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProxyCall_Remover.Deobfuscation
{
    internal class ProxyCallRemover : IDeobfuscator
    {
        string IDeobfuscator.Name => "Proxy Call Remover";

        private int RemovedProxyCalls = 0;

        private readonly Dictionary<TypeDef, List<MethodDef>> JunksMethods = new Dictionary<TypeDef, List<MethodDef>>();

        private ModuleDef _module;

        int IDeobfuscator.GetResult()
        {
            return RemovedProxyCalls;
        }

        void IDeobfuscator.RemoveProtection(ModuleDef module)
        {
            _module = module;

            RemoveProxyCalls();

            RemoveJunksMethods();
        }

        void IDeobfuscator.Dispose()
        {
            _module = null;
            JunksMethods.Clear();
            RemovedProxyCalls = 0;
        }

        private void RemoveProxyCalls()
        {
            foreach (TypeDef typeDef in _module.GetTypes())
            {
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (methodDef.HasBody)
                    {
                        ProcessMethod(typeDef, methodDef);
                    }
                }
            }
        }

        private void RemoveJunksMethods()
        {
            if (OptionsUnpack.IsEnabledRemoveJunks)
            {
                foreach (TypeDef typeDef in _module.GetTypes())
                {
                    if (JunksMethods.ContainsKey(typeDef))
                    {
                        var list = JunksMethods[typeDef];
                        foreach (var method in list)
                        {
                            typeDef.Remove(method);
                        }
                    }
                }
            }
        }

        private void ProcessMethod(TypeDef typeDef, MethodDef method)
        {
            IList<Instruction> instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                try
                {
                    Instruction instruction = instructions[i];
                    if (instruction.OpCode.Equals(OpCodes.Call))
                    {
                        MethodDef methodDef2 = instruction.Operand as MethodDef;
                        if (IsProxyCallMethod(typeDef, methodDef2))
                        {
                            bool IsValid = GetProxyData(methodDef2, out OpCode opCode, out object operand);
                            if (IsValid)
                            {
                                instruction.OpCode = opCode;
                                instruction.Operand = operand;

                                RemovedProxyCalls++;

                                if (!JunksMethods.ContainsKey(typeDef))
                                    JunksMethods.Add(typeDef, new List<MethodDef>());

                                var list = JunksMethods[typeDef];
                                if (!list.Contains(methodDef2))
                                    list.Add(methodDef2);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.Output(method.Name + " " + ex);
                }
            }
        }

        private bool GetProxyData(MethodDef method, out OpCode opCode, out object operand)
        {
            opCode = null;
            operand = null;
            if (!method.HasBody)
            {
                return false;
            }
            Instruction[] array = method.Body.Instructions.ToArray();
            int num = array.Length;
            if (array.Length <= 1)
            {
                return false;
            }
            try
            {
                if (array[num - 2].OpCode.Equals(OpCodes.Newobj))
                {
                    opCode = array[num - 2].OpCode;
                    operand = array[num - 2].Operand;
                }
                if (array[num - 2].OpCode.Equals(OpCodes.Call))
                {
                    opCode = array[num - 2].OpCode;
                    operand = array[num - 2].Operand;
                }
                if (array[num - 2].OpCode.Equals(OpCodes.Callvirt))
                {
                    opCode = array[num - 2].OpCode;
                    operand = array[num - 2].Operand;
                }
                if (array[num - 1].OpCode.Code == Code.Ret)
                {
                    if (num != method.Parameters.Count + 2)
                    {
                        return false;
                    }
                    opCode = array[num - 2].OpCode;
                    operand = array[num - 2].Operand;
                }

                if (opCode != null)
                    return true;
            }
            catch
            {
            }
            return false;
        }

        private bool IsProxyCallMethod(TypeDef typeDef, MethodDef method)
        {
            return method?.IsStatic == true && typeDef.Methods.Contains(method);
        }
    }
}