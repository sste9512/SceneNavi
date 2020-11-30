using System;
using System.Reflection;
using System.Windows.Forms;
using Autofac;
using Autofac.Core;
using MediatR;
using Ninject;
using Ninject.Modules;
using OpenTK.Platform.Windows;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using Unity;
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

        Container = new UnityContainer();
        Container.ConfigureClasses();
        return Container;
    }


    private static void ConfigureClasses(this IUnityContainer container)
    {
        Get().RegisterType<IRomHandler, BaseRomHandler>(new ContainerControlledLifetimeManager());
        Get().RegisterType<IHeaderParent, SceneTableEntryOcarina>();
    }

    public static T Resolve<T>()
    {
        return Get().Resolve<T>();
    }
}


public static class Navigation
{
    public static void Move<T>() where T : Form
    {
        try
        {
            Di.Resolve<T>().Show();
        }
        catch (Exception ex)
        {
            Console.Write(ex.Message);
        }
    }

    public static T ShowModal<T>() where T : Form, new()
    {
        try
        {
            var modal = Di.Resolve<T>();
            return modal;
        }
        catch (Exception ex)
        {
            Console.Write(ex.Message);
            return new T();
        }
    }
}