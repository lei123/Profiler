﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using NServiceBus.Profiler.Common;
using log4net;
using Module = Autofac.Module;

namespace NServiceBus.Profiler.Desktop.Startup
{
    public static class BuilderExtensions
    {
        private static readonly ILog Logger = LogManager.GetLogger("IoC");

        internal static Func<string, ILog, Assembly> LoadModuleAssembly = (file, logger) =>
        {
            const string errorFormat = "Plugin assembly {0} was not loaded. The reason is: {1}";

            try
            {
                return Assembly.LoadFile(file);
            }
            catch (ReflectionTypeLoadException ex)
            {
                logger.WarnFormat(errorFormat, file, ex.LoaderExceptions[0].GetBaseException().Message);
            }
            catch (Exception ex)
            {
                logger.WarnFormat(errorFormat, file, ex.GetBaseException().Message);
            }

            return null;
        };

        public static void RegisterPluginModules(this ContainerBuilder builder, string folder, string filePattern)
        {
            Guard.NotNullOrEmpty(() => folder, folder);
            Guard.NotNullOrEmpty(() => filePattern, filePattern);

            if (!Directory.Exists(folder))
            {
                Logger.WarnFormat("Plugin folders was not found at {0}", folder);
                return;
            }

            foreach (var fullPath in Directory.GetFiles(folder, filePattern))
            {
                var assembly = LoadModuleAssembly(fullPath, Logger);
                if (assembly != null)
                {
                    RegisterModules(builder, assembly);
                }
            }
        }

        public static void RegisterModules(this ContainerBuilder builder, Assembly assembly)
        {
            Guard.NotNull(() => builder, builder);
            Guard.NotNull(() => assembly, assembly);

            foreach (var type in assembly.GetExportedTypes())
            {
                if(type.IsAssignableTo<Module>())
                {
                    var module = (Module)Activator.CreateInstance(type);
                    builder.RegisterModule(module);
                }
                else if(type.IsViewOrViewModel())
                {
                    builder.RegisterType(type).AsImplementedInterfaces().SingleInstance();
                }
            }
        }

        public static bool IsViewOrViewModel(this Type type)
        {
            return type != null &&
                   type.IsClass &&
                   !type.IsAbstract &&
                   type.Namespace != null &&
                   MatchingNamespaces.Any(ns => type.Name.EndsWith(ns, StringComparison.InvariantCultureIgnoreCase));
        }

        private static IEnumerable<string> MatchingNamespaces
        {
            get
            {
                yield return "ViewModel";
                yield return "View";
            }
        }
    }
}