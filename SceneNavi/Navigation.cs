using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using MediatR;
using NLog;
using Unity;

namespace SceneNavi
{
    public interface INavigationRepository : INotifyPropertyChanged
    {
        IDictionary<Guid, WeakReference<Form>> ActiveForms { get; set; }
    }

    public class NavigationRepository : INavigationRepository
    {
        public IDictionary<Guid, WeakReference<Form>> ActiveForms { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class Navigation : INavigation
    {
       
        private readonly IUnityContainer _container;
        private readonly ILogger _logger;
        private readonly IMediator _mediator;
        private readonly INavigationRepository _navigationRepository;


        public Navigation(IUnityContainer container, ILogger logger, IMediator mediator,
            INavigationRepository navigationRepository)
        {
            _container = container;
            _logger = logger;
            _mediator = mediator;
            _navigationRepository = navigationRepository;

            _navigationRepository.PropertyChanged += NavigationRepositoryOnPropertyChanged;
        }

        private void NavigationRepositoryOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine(sender.GetType());
            Console.WriteLine(e.PropertyName);
            Console.WriteLine(e.GetType());
        }


        public void Move<T>() where T : Form
        {
            try
            {
                var form = _container.Resolve<T>();
                _navigationRepository.ActiveForms.Add(Guid.NewGuid(), new WeakReference<Form>(form));
                _logger.Log(LogLevel.Info, "Showing Form of" + nameof(form));
                form.Show();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                Console.Write(ex.Message);
            }
        }

        public T ShowModal<T>() where T : Form, new()
        {
            try
            {
                var modal = _container.Resolve<T>();
                _logger.Log(LogLevel.Info, "Showing Modal of" + nameof(modal));
                return modal;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new T();
            }
        }
    }
}