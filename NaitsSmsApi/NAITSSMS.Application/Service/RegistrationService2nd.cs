using AutoMapper;
using NAITSSMS.Application.DTOs;
using NAITSSMS.Application.IService;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using NAITSSMS.Infrastructure.Data.Security;
using NAITSSMS.Infrastructure.Repo;
using NAITSSMS.Infrastructure.Unitofwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Application.Service
{
    public class RegistrationService2nd
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        //private readonly IRegistrationService _registrationService;

        public RegistrationService2nd(
            IUnitOfWork uow,
            IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }
    }
}
