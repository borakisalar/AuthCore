using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using StajBackendProject.Controllers;
using StajBackendProject.Interfaces;
using StajBackendProject.Models;
using StajBackendProject.Models.Dto;
using Xunit;

namespace StajBackendProject.Tests.Controllers
{
    public class UsersControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly UsersController _controller;

        public UsersControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _mockTokenService = new Mock<ITokenService>();
            _controller = new UsersController(_mockUserService.Object, _mockTokenService.Object);
        }

        private void SetupHttpContext(string nameIdentifier = "1", string role = "Admin")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, nameIdentifier),
                new Claim(ClaimTypes.Role, role)
            };

            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
            var context = new DefaultHttpContext { User = user };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = context
            };
        }

        [Fact]
        public async Task GetAllUsers_ReturnsOk_WithUsersList()
        {
            var users = new List<UserResponseDto> { new UserResponseDto { Id = 1 } };
            _mockUserService.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(users);

            var result = await _controller.GetAllUsers();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(users);
        }

        [Fact]
        public async Task GetUsersById_ExistingId_ReturnsOk()
        {
            var user = new UserResponseDto { Id = 1 };
            _mockUserService.Setup(s => s.GetUserByIdAsync(1)).ReturnsAsync(new User { Id = 1 });

            var result = await _controller.GetUsersById(1);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        }

        [Fact]
        public async Task GetUsersById_NonExistingId_ReturnsNotFound()
        {
            _mockUserService.Setup(s => s.GetUserByIdAsync(99)).ReturnsAsync((User)null);

            var result = await _controller.GetUsersById(99);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task CreateUserByAdmin_ValidDto_ReturnsOk()
        {
            var dto = new AddNewUserDto { Email = "admin@test.com" };
            _mockUserService.Setup(s => s.AddNewUserAsync(dto)).Returns(Task.CompletedTask);

            var result = await _controller.CreateUserByAdmin(dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void DeleteUser_ExistingId_ReturnsOk()
        {
            _mockUserService.Setup(s => s.DeleteUser(1)).Returns(true);

            var result = _controller.DeleteUser(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void DeleteUser_NonExistingId_ReturnsNotFound()
        {
            _mockUserService.Setup(s => s.DeleteUser(99)).Returns(false);

            var result = _controller.DeleteUser(99);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public void SoftDeleteUserById_ExistingId_ReturnsOk()
        {
            _mockUserService.Setup(s => s.SoftDeleteUserById(1)).Returns(true);

            var result = _controller.SoftDeleteUserById(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void SoftDeleteUserById_NonExistingId_ReturnsNotFound()
        {
            _mockUserService.Setup(s => s.SoftDeleteUserById(99)).Returns(false);

            var result = _controller.SoftDeleteUserById(99);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public void ActivateUser_ExistingId_ReturnsOk()
        {
            _mockUserService.Setup(s => s.ActivateUser(1)).Returns(true);

            var result = _controller.ActivateUser(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void ActivateUser_NonExistingId_ReturnsNotFound()
        {
            _mockUserService.Setup(s => s.ActivateUser(99)).Returns(false);

            var result = _controller.ActivateUser(99);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public void GetAllUsersOrderByDate_ReturnsOk_WithUsersList()
        {
            var users = new List<User> { new User { Id = 1 } };
            _mockUserService.Setup(s => s.GetAllUsersOrderByDate()).Returns(users);

            var result = _controller.GetAllUsersOrderByDate();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(users);
        }

        [Fact]
        public void GetMyTokenInfo_ValidToken_ReturnsOk()
        {
            SetupHttpContext();

            var result = _controller.GetMyTokenInfo();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void AssignRole_ValidRequest_ReturnsOk()
        {
            var dto = new AssignRoleDto { RoleId = 1 };
            _mockUserService.Setup(s => s.AssignRoleToUser(1, 1)).Returns(true);

            var result = _controller.AssignRole(1, dto);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void RemoveRole_ValidRequest_ReturnsOk()
        {
            _mockUserService.Setup(s => s.RemoveRoleFromUser(1, 1)).Returns(true);

            var result = _controller.RemoveRole(1, 1);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetUsersByRole_ExistingRole_ReturnsOk()
        {
            var users = new List<UserRoleSpResponseDto> { new UserRoleSpResponseDto { Id = 1 } };
            _mockUserService.Setup(s => s.GetUsersByRoleNameSpAsync("Admin")).ReturnsAsync(users);

            var result = await _controller.GetUsersByRole("Admin");

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(users);
        }

        [Fact]
        public async Task GetUsersByRole_NonExistingRole_ReturnsNotFound()
        {
            _mockUserService.Setup(s => s.GetUsersByRoleNameSpAsync("Unknown")).ReturnsAsync(new List<UserRoleSpResponseDto>());

            var result = await _controller.GetUsersByRole("Unknown");

            result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}
