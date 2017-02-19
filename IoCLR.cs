using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace io
{
    public class IoCLR : IoObject
    {
        public override string name { get { return "CLR"; } }
        public Dictionary<string, IoCLRAssembly> usingNamespaces = new Dictionary<string, IoCLRAssembly>();
        public Dictionary<string, IoCLRAssembly> loadedAssemblies = new Dictionary<string, IoCLRAssembly>();
        public IoCLR() : base() { isActivatable = true; }

        public new static IoCLR createProto(IoState state)
        {
            IoCLR cf = new IoCLR();
            return cf.proto(state) as IoCLR;
        }

        public new static IoCLR createObject(IoState state)
        {
            IoCLR cf = new IoCLR();
            return cf.proto(state).clone(state) as IoCLR;
        }

        public IoCLR(IoState state, string name)
        {
            isActivatable = true;
            this.state = state;
            createSlots();
            createProtos();
            uniqueId = 0;
        }

        public override IoObject proto(IoState state)
        {
            IoCLR pro = new IoCLR();
            pro.state = state;
            pro.uniqueId = 0;
            pro.createSlots();
            pro.createProtos();
            pro.isActivatable = true;
            state.registerProtoWithFunc(pro.name, new IoStateProto(pro.name, pro, new IoStateProtoFunc(pro.proto)));
            //pro.protos.Add(state.protoWithInitFunc("Object"));

            IoCFunction[] methodTable = new IoCFunction[] {
                new IoCFunction("loadAssembly", new IoMethodFunc(IoCLR.slotLoadAssembly)),
                new IoCFunction("using", new IoMethodFunc(IoCLR.slotUsing)),
                new IoCFunction("getType", new IoMethodFunc(IoCLR.slotGetType)),
			};

            pro.addTaglessMethodTable(state, methodTable);
            return pro;
        }

        public override void cloneSpecific(IoObject from, IoObject to)
        {
            to.isActivatable = true;
        }

        // Published Slots

        public static IoObject slotUsing(IoObject target, IoObject locals, IoObject message)
        {
            IoCLR self = target as IoCLR;
            IoMessage m = message as IoMessage;
            IoSeq nameSpace = m.localsSymbolArgAt(locals, 0);
            bool validNamespace = false;
            IoCLRAssembly foundInAssembly = null;
            foreach (IoCLRAssembly asm in self.loadedAssemblies.Values)
            {
                if (asm.assemblyNamespaces.ContainsKey(nameSpace.value))
                {
                    if (asm.assemblyNamespaces[nameSpace.value]!=null)
                    {
                        validNamespace = true;
                        foundInAssembly = asm;
                        break;
                    }
                }
            }
            if (!validNamespace)
            {
                Console.WriteLine("Namespace '{0}' is not valid.", nameSpace.value);
                return self;
            }

            if (!self.usingNamespaces.ContainsKey(nameSpace.value))
                self.usingNamespaces[nameSpace.value] = foundInAssembly;
            return self;
        }

        public static IoObject slotLoadAssembly(IoObject target, IoObject locals, IoObject message)
        {
            IoCLR self = target as IoCLR;
            IoMessage m = message as IoMessage;
            IoSeq assemblyName = m.localsSymbolArgAt(locals, 0);
            IoCLRAssembly asm = null;
            if (self.loadedAssemblies.ContainsKey(assemblyName.value))
            {
                asm = self.loadedAssemblies[assemblyName.value];
                if (asm != null)
                {
                    return asm;
                }
            }

            asm = IoCLRAssembly.createObject(target.state);
            var name = new AssemblyName(assemblyName.value);
            try
            {
                asm.assembly = Assembly.Load(name);
                Console.WriteLine("载入{0} ....成功！", name.Name);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("载入{0} ....失败！", name.Name);
            }

            if (asm.assembly == null) return self;

            self.loadedAssemblies[assemblyName.value] = asm;

            asm.assemblyTypes = asm.assembly.GetTypes();
            asm.assemblyNamespaces = new Dictionary<string, Dictionary<string, Type>>();
            foreach (Type t in asm.assemblyTypes)
            {
                string theNameSpace = t.FullName.LastIndexOf(".") == -1 ? "-" : t.FullName.Substring(0, t.FullName.LastIndexOf("."));
                string theClass = t.FullName.LastIndexOf(".") == -1 ? t.FullName : t.FullName.Substring(t.FullName.LastIndexOf(".") + 1);
                if (theClass.Equals("Form"))
                {
                    //~//int i = 0;
                }

                if (asm.assemblyNamespaces.ContainsKey(theNameSpace))
                {
                    Dictionary<string, Type> a = asm.assemblyNamespaces[theNameSpace];
                    a[theClass] = t;
                }
                else
                {
                    Dictionary<string, Type> classes = new Dictionary<string, Type>();
                    classes[theClass] = t;
                    asm.assemblyNamespaces[theNameSpace] = classes;
                }
            }
            return asm;
        }

        public static IoObject slotGetType(IoObject target, IoObject locals, IoObject message)
        {
            IoCLR self = target as IoCLR;
            IoMessage m = message as IoMessage;
            IoSeq typeName = m.localsSymbolArgAt(locals, 0);
            IoObject obj = self.getType(target.state, typeName.value);
            return obj == null ? target.state.ioNil : obj;
        }

        // Public methos

        public IoObject getType(IoState state, string typeName)
        {
            Type t = null;
            foreach (string s in this.usingNamespaces.Keys)
            {
                IoCLRAssembly asm = this.usingNamespaces[s] as IoCLRAssembly;
                t = asm.assembly.GetType(s + "." + typeName);
                if (t != null)
                {
                    IoCLRObject obj = IoCLRObject.createObject(state) as IoCLRObject;
                    obj.clrType = t;
                    obj.clrInstance = null;
                    return obj;
                }
            }
            if (t == null)
            {
                foreach (string s in this.loadedAssemblies.Keys)
                {
                    IoCLRAssembly asm = this.loadedAssemblies[s] as IoCLRAssembly;
                    t = asm.assembly.GetType(typeName);
                    if (t != null)
                    {
                        IoCLRObject obj = IoCLRObject.createObject(state) as IoCLRObject;
                        obj.clrType = t;
                        obj.clrInstance = null;
                        return obj;
                    }
                }
            }
            return null;
        }

        public IoCLRObject getType(string typeName)
        {
            Type t = null;
            foreach (string s in this.usingNamespaces.Keys)
            {
                IoCLRAssembly asm = this.usingNamespaces[s] as IoCLRAssembly;
                t = asm.assembly.GetType(s + typeName);
                if (t != null)
                {
                    IoCLRObject obj = new IoCLRObject();
                    obj.clrType = t;
                    obj.clrInstance = null;
                }
            }
            return null;
        }

        public override IoObject activate(IoObject self, IoObject target, IoObject locals, IoMessage m, IoObject slotContext)
        {
            return self;
        }
    }
}
