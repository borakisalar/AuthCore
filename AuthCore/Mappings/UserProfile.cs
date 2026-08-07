using AutoMapper;
using AuthCore.Models;
using AuthCore.Models.Dto;

namespace AuthCore.Mappings
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserResponseDto>()
                .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.Roles.Select(r => r.Name)));
        }
    }
}
