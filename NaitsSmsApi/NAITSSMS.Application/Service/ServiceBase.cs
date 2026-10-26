using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Naits.Common.DependencyInjection.Locator;
using Naits.Common.DependencyInjection.Resolver;
using Naits.Common.Shared;
using NAITSSMS.Infrastructure.Unitofwork;
using NAITSSMS.Application.IService;

namespace NAITSSMS.Application.Service
{
    public class ServiceBase : IServiceBase
    {
        protected IServiceProvider ServiceLocator;
        protected readonly IUnitOfWork UnitOfWork;

        private IServiceProvider _provider;
        protected ServiceBase(IServiceProvider provider)
        {
            ServiceLocator = provider;
            UnitOfWork = ServiceLocator.GetService(typeof(IUnitOfWork)) as IUnitOfWork;
        }
    }
}
