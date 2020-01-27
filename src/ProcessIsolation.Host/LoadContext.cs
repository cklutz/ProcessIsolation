using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace ProcessIsolation.Host
{
    internal class LoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, Assembly> m_shareAssemblies = new Dictionary<string, Assembly>(
            StringComparer.OrdinalIgnoreCase);
        private readonly AssemblyDependencyResolver m_resolver;

        public LoadContext(string componentAssemblyPath, IEnumerable<Type> sharedTypes = null)
        {
            if (componentAssemblyPath == null)
                throw new ArgumentNullException(nameof(componentAssemblyPath));

            m_resolver = new AssemblyDependencyResolver(componentAssemblyPath);

            if (sharedTypes != null)
            {
                foreach (var type in sharedTypes)
                {
                    m_shareAssemblies[Path.GetFileName(type.Assembly.Location)] = type.Assembly;
                }
            }
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string fileName = assemblyName.Name + ".dll";
            if (m_shareAssemblies.TryGetValue(fileName, out var sharedAssembly))
            {
                return sharedAssembly;
            }

            string path = m_resolver.ResolveAssemblyToPath(assemblyName);
            if (path != null)
            {
                return LoadFromAssemblyPath(path);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = m_resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

}
