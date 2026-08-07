using AutoMapper;
using AuthCore.Interfaces;
using AuthCore.Models;
using AuthCore.Models.Dto;

namespace AuthCore.Implements
{
    public class RoleService : IRoleService
    {
        private readonly UsersContext _context;
        private readonly IMapper _mapper;

        public RoleService(UsersContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public List<RoleResponseDto> GetAllRoles()
        {
            var roles = _context.Roles.ToList();
            return _mapper.Map<List<RoleResponseDto>>(roles);
        }
    }
}