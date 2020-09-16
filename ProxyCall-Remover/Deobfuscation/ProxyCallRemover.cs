using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProxyCall_Remover.Deobfuscation
{
    internal class ProxyCallRemover : IDeobfuscator
    {
        string IDeobfuscator.Name => "Proxy Call Remover";

        private bool IsAdvanced = true;

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
            int currentCount = 0;

        repeat:
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

            if (currentCount == 0 && currentCount != RemovedProxyCalls)
            {
                currentCount = RemovedProxyCalls;
                goto repeat;
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
#if !DEBUG
                try
                {
#endif
                Instruction instruction = instructions[i];
                if (instruction.OpCode.Equals(OpCodes.Call))
                {
                    MethodDef methodDef2 = instruction.Operand as MethodDef;
                    if (IsProxyCallMethod(typeDef, methodDef2))
                    {
                        bool IsValid = GetProxyData(typeDef, methodDef2, out OpCode opCode, out object operand);
                        if (IsValid)
                        {
                            instruction.OpCode = opCode;
                            instruction.Operand = operand;

                            method.Body.KeepOldMaxStack = true;

                            RemovedProxyCalls++;

                            AddJunkMethod(typeDef, methodDef2);
                        }
                    }
                }
#if !DEBUG
            }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    MainWindow.Output(method.Name + " " + ex);
                }
#endif
            }

        }

        private void AddJunkMethod(TypeDef type, MethodDef method)
        {
            if (!JunksMethods.ContainsKey(type))
                JunksMethods.Add(type, new List<MethodDef>());

            var list = JunksMethods[type];
            if (!list.Contains(method))
                list.Add(method);
        }

        private bool GetProxyData(TypeDef type, MethodDef method, out OpCode opCode, out object operand)
        {
            int arguments = method.Parameters.Count;
            opCode = null;
            operand = null;

            if (!method.HasBody)
            {
                return false;
            }

            Instruction[] array = method.Body.Instructions.ToArray();

            int length = array.Length;

            if (length <= 1)
            {
                return false;
            }

            if (IsProxyCallMethod(type, method) == false)
            {
                Debug.Assert(false);
            }


            if (GetData(type, method, array, out opCode, out operand))
            {
                return true;
            }

            else if (IsAdvanced == true)
            {
                Instruction arg1;
                Instruction arg2;
                Instruction call;

                for (int a = arguments; a < method.Body.Instructions.Count; a++)
                {
                    call = method.Body.Instructions[a];

                    if (arguments == 2)
                    {
                        arg1 = method.Body.Instructions[a - 2];
                        arg2 = method.Body.Instructions[a - 1];

                        if (GetDataWithInst(type, method, array, call, out opCode, out operand))
                        {
                            return true;
                        }
                    }
                    else if (arguments == 1)
                    {
                        arg1 = method.Body.Instructions[a - 1];

                        if (GetDataWithInst(type, method, array, call, out opCode, out operand))
                        {
                            return true;
                        }

                    }
                    else
                    {
                        if (GetDataWithInst(type, method, array, call, out opCode, out operand))
                        {
                            return true;
                        }
                    }
                }
            }


            if (opCode != null)
                return true;

            return false;
        }

        private bool GetData(TypeDef type, MethodDef method, Instruction[] array, out OpCode opCode, out object operand)
        {
            opCode = null;
            operand = null;
            int length = array.Length;

            if (array[length - 2].OpCode.Equals(OpCodes.Newobj))
            {
                opCode = array[length - 2].OpCode;
                operand = array[length - 2].Operand;
            }
            else if (array[length - 2].OpCode.Equals(OpCodes.Call))
            {
                var secondMethod = array[length - 2].Operand as MethodDef;

                if (IsProxyCallMethod(type, secondMethod))
                {
                    bool IsValid = GetProxyData(type, secondMethod, out opCode, out operand);
                    if (IsValid)
                    {
                        AddJunkMethod(type, secondMethod);
                        return true;
                    }
                }
                else
                {
                    opCode = array[length - 2].OpCode;
                    operand = array[length - 2].Operand;
                    return true;
                }

            }
            else if (array[length - 2].OpCode.Equals(OpCodes.Callvirt))
            {
                opCode = array[length - 2].OpCode;
                operand = array[length - 2].Operand;
            }
            else if (array[length - 1].OpCode.Code == Code.Ret)
            {
                if (length != method.Parameters.Count + 2)
                {
                    return false;
                }
                opCode = array[length - 2].OpCode;
                operand = array[length - 2].Operand;
            }

            Debug.Assert(opCode != OpCodes.Nop);

            if (opCode != null)
                return true;

            return false;
        }

        private bool GetDataWithInst(TypeDef type, MethodDef method, Instruction[] array, Instruction inst, out OpCode opCode, out object operand)
        {
            opCode = null;
            operand = null;
            int length = array.Length;

            if (IsProxyCallMethod(type, method) == false)
            {
                Debug.Assert(false);
            }

            if (inst.OpCode.Equals(OpCodes.Newobj))
            {
                opCode = inst.OpCode;
                operand = inst.Operand;
            }
            else if (inst.OpCode.Equals(OpCodes.Call))
            {
                var secondMethod = inst.Operand as MethodDef;

                if (IsProxyCallMethod(type, secondMethod))
                {
                    bool IsValid = GetProxyData(type, secondMethod, out opCode, out operand);
                    if (IsValid)
                    {
                        return true;
                    }
                }
                else
                {
                    opCode = inst.OpCode;
                    operand = inst.Operand;
                    return true;
                }

            }
            else if (inst.OpCode.Equals(OpCodes.Callvirt))
            {
                opCode = inst.OpCode;
                operand = inst.Operand;
            }



            if (opCode != null)
                return true;

            return false;
        }

        private bool IsProxyCallMethod(TypeDef typeDef, MethodDef method)
        {
            if (method == null || method.HasBody == false)
                return false;

            int countLogics = method.Body.Instructions.Count(x => x.OpCode == OpCodes.Call || x.OpCode == OpCodes.Newobj || x.OpCode == OpCodes.Callvirt);

            if (countLogics > 1)
                return false;

            bool NotStatic = method?.IsStatic == false && typeDef.IsAbstract == false && typeDef.IsSealed == false;

            return method?.IsStatic == true || NotStatic && typeDef.Methods.Contains(method);
        }
    }
}