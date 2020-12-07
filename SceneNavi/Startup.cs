using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows.Forms;
using Autofac;
using Autofac.Core;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using Config.Net;
using MediatR;
using MediatR.Pipeline;
using Ninject;
using Ninject.Modules;
using NLog;
using OpenTK.Platform.Windows;
using SceneNavi;
using SceneNavi.Configurations;
using SceneNavi.Dependencies.Implementations;
using SceneNavi.Dependencies.Interfaces;
using SceneNavi.HeaderCommands;
using SceneNavi.Repository;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.Services.Commands;
using SceneNavi.Utilities.OpenGLHelpers;
using Unity;
using Unity.Injection;
using Unity.Lifetime;

namespace SceneNavi.Startup
{
    public class Startup
    {
        public Startup()
        {
        }
    }
}

public static class Di
{
    private static IUnityContainer Container { get; set; }

    private static IUnityContainer Get()
    {
        if (Container != null) return Container;

        Container.ConfigureClasses();
        return Container;
    }


    private static void ConfigureClasses(this IUnityContainer container)
    {
        Container = new UnityContainer();
        Container.AddExtension(new Diagnostic());

        Container.RegisterInstance(Container);

        Container.RegisterType<IRomHandler, BaseRomHandler>(new ContainerControlledLifetimeManager());
        Container.RegisterType<IHeaderParent, SceneTableEntryOcarina>();

        Container.RegisterType<INotificationHandler<MessageBoxCommand>, MessageboxHandler>();
        Container.RegisterType<IRequestHandler<MessageBoxRequest>, MessageBoxQueryHandler>();

        Container.RegisterConfigurations();

        Container.RegisterType<DbContext, RomActorDbContext>();
   

        Container.RegisterType<ICamera, Camera>();
        Container.RegisterType<ITextPrinter, TextPrinter>();
        Container.RegisterType<IFpsMonitor, FpsMonitor>();

        Container.RegisterType<INavigationRepository, NavigationRepository>();
        Container.RegisterType<INavigation, Navigation>();
        Container.RegisterType<ILogger, Logger>();
        Container.RegisterMediator(new HierarchicalLifetimeManager());
        Container.RegisterMediatorHandlers(Assembly.GetAssembly(typeof(IRomHandler)));


        Container.RegisterType<Form, MainForm>(nameof(MainForm));
        Container.RegisterType<Form, TitleCardForm>(nameof(TitleCardForm));

        Container.RegisterType<PositionState>();
        Container.RegisterType<Generic>();
        Container.RegisterType<Actors>();
        Container.RegisterType<HeaderLoader>();
        Container.RegisterType<MeshHeader>();
    }

    private static void RegisterConfigurations(this IUnityContainer container)
    {
        Container.RegisterInstance(new ConfigurationBuilder<IMainFormConfig>()
            .UseJsonConfig(@"C:/Configs/MainFormConfig")
            .Build());

        Container.RegisterInstance(new ConfigurationBuilder<IBaseConfig>()
            .UseJsonConfig(@"Configs/BaseConfig")
            .Build());

        Container.RegisterInstance(new ConfigurationBuilder<ITitleCardFormSettings>()
            .UseJsonConfig(@"Configs/TitleCardFromSettings")
            .Build());

        Container.RegisterInstance(new ConfigurationBuilder<ICameraSettings>()
            .UseJsonConfig(@"Configs/CameraSettings")
            .Build());

//        Container.RegisterInstance(new ConfigurationBuilder<ITextPrinterSettings>()
//            .UseJsonConfig()
//            .Build());

        Container.RegisterInstance(new ConfigurationBuilder<IGraphicsRenderingSettings>()
            .UseJsonConfig(@"Configs/GraphicsRenderingSettings")
            .Build());

        Container.RegisterInstance(new ConfigurationBuilder<IViewPortRenderSettings>()
            .UseJsonConfig(@"Configs/ViewPortSettings")
            .Build());
    }


    private static IUnityContainer RegisterMediator(this IUnityContainer container, ITypeLifetimeManager lifetimeManager)
    {
        return container.RegisterType<IMediator, Mediator>(lifetimeManager)
            .RegisterInstance<ServiceFactory>(type =>
            {
                var enumerableType = type
                    .GetInterfaces()
                    .Concat(new[] {type})
                    .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

                return enumerableType != null
                    ? container.ResolveAll(enumerableType.GetGenericArguments()[0])
                    : container.IsRegistered(type)
                        ? container.Resolve(type)
                        : null;
            });
    }

    private static IUnityContainer RegisterMediatorHandlers(this IUnityContainer container, Assembly assembly)
    {
        return container.RegisterTypesImplementingType(assembly, typeof(IRequestHandler<,>))
            .RegisterNamedTypesImplementingType(assembly, typeof(INotificationHandler<>));
    }

    private static IUnityContainer RegisterTypesImplementingType(this IUnityContainer container, Assembly assembly,
        Type type)
    {
        foreach (var implementation in assembly.GetTypes().Where(t =>
            t.GetInterfaces().Any(implementation => IsSubclassOfRawGeneric(type, implementation))))
        {
            var interfaces = implementation.GetInterfaces();
            foreach (var @interface in interfaces)
                container.RegisterType(@interface, implementation);
        }

        return container;
    }

    private static IUnityContainer RegisterNamedTypesImplementingType(this IUnityContainer container, Assembly assembly,
        Type type)
    {
        foreach (var implementation in assembly.GetTypes().Where(t =>
            t.GetInterfaces().Any(implementation => IsSubclassOfRawGeneric(type, implementation))))
        {
            var interfaces = implementation.GetInterfaces();
            foreach (var @interface in interfaces)
                container.RegisterType(@interface, implementation, implementation.FullName);
        }

        return container;
    }

    private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
    {
        while (toCheck != null && toCheck != typeof(object))
        {
            var currentType = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
            if (generic == currentType)
                return true;

            toCheck = toCheck.BaseType;
        }

        return false;
    }

    public static T Resolve<T>()
    {
        return Get().Resolve<T>();
    }

    public static T Resolve<T>(string id)
    {
        return Get().Resolve<T>(id);
    }
}