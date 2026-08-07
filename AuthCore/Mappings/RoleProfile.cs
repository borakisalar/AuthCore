using AutoMapper;
using AuthCore.Models;
using AuthCore.Models.Dto;

namespace AuthCore.Mappings
{
    public class RoleProfile : Profile
    {
        public RoleProfile() 
        {
            CreateMap<Role, RoleResponseDto>();
        }
    }
}
