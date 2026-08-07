using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FluentAssertions;
using AuthCore.Implements;
using AuthCore.Models;
using AuthCore.Models.Dto;

namespace AuthCore.Tests.Services
{
    public class RoleServiceTests : IDisposable
    {
        private readonly UsersContext _context;
        private readonly Mock<IMapper> _mapperMock;
        private readonly RoleService _roleService;

        public RoleServiceTests()
        {
            var options = new DbContextOptionsBuilder<UsersContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new UsersContext(options);
            _mapperMock = new Mock<IMapper>();

            _roleService = new RoleService(_context, _mapperMock.Object);
        }

        [Fact]
        public void GetAllRoles_ShouldReturnEmptyList_WhenNoRolesExist()
        {
            _mapperMock.Setup(m => m.Map<List<RoleResponseDto>>(It.IsAny<List<Role>>()))
                .Returns(new List<RoleResponseDto>());

            var result = _roleService.GetAllRoles();

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetAllRoles_ShouldReturnSingleRole_WhenOneRoleExists()
        {
            var role = new Role { Id = 1, Name = "Admin" };
            _context.Roles.Add(role);
            _context.SaveChanges();

            var dtoList = new List<RoleResponseDto> { new RoleResponseDto { Id = 1, Name = "Admin" } };
            _mapperMock.Setup(m => m.Map<List<RoleResponseDto>>(It.Is<List<Role>>(r => r.Count == 1)))
                .Returns(dtoList);

            var result = _roleService.GetAllRoles();

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Admin");
        }

        [Fact]
        public void GetAllRoles_ShouldReturnMultipleRoles_WhenMultipleRolesExist()
        {
            var roles = new List<Role>
            {
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "User" }
            };
            _context.Roles.AddRange(roles);
            _context.SaveChanges();

            var dtoList = new List<RoleResponseDto>
            {
                new RoleResponseDto { Id = 1, Name = "Admin" },
                new RoleResponseDto { Id = 2, Name = "User" }
            };
            _mapperMock.Setup(m => m.Map<List<RoleResponseDto>>(It.Is<List<Role>>(r => r.Count == 2)))
                .Returns(dtoList);

            var result = _roleService.GetAllRoles();

            result.Should().NotBeNull();
            result.Should().HaveCount(2);
        }

        [Fact]
        public void GetAllRoles_ShouldCallMapperOnce()
        {
            var result = _roleService.GetAllRoles();

            _mapperMock.Verify(m => m.Map<List<RoleResponseDto>>(It.IsAny<List<Role>>()), Times.Once);
        }

        [Fact]
        public void GetAllRoles_ShouldReturnCorrectTypes()
        {
            _mapperMock.Setup(m => m.Map<List<RoleResponseDto>>(It.IsAny<List<Role>>()))
                .Returns(new List<RoleResponseDto>());

            var result = _roleService.GetAllRoles();

            result.Should().BeAssignableTo<List<RoleResponseDto>>();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
