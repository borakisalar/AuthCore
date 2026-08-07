using System;
using Moq;
using Xunit;
using FluentAssertions;
using AuthCore.Implements;
using AuthCore.Interfaces;

namespace AuthCore.Tests.Services
{
    public class PasswordHasherTests
    {
        private readonly PasswordHasher _passwordHasher;

        public PasswordHasherTests()
        {
            _passwordHasher = new PasswordHasher();
        }

        [Fact]
        public void Hash_ShouldReturnNonNullOrEmptyString()
        {
            var result = _passwordHasher.Hash("mySecretPassword");
            result.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Hash_ShouldReturnDifferentValueFromPlaintext()
        {
            var password = "mySecretPassword";
            var result = _passwordHasher.Hash(password);
            result.Should().NotBe(password);
        }

        [Fact]
        public void Hash_ShouldReturnDifferentHashesForSamePassword()
        {
            var password = "mySecretPassword";
            var hash1 = _passwordHasher.Hash(password);
            var hash2 = _passwordHasher.Hash(password);
            
            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void Hash_ShouldNotReturnNullForEmptyPassword()
        {
            var result = _passwordHasher.Hash("");
            result.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Verify_ShouldReturnTrueForCorrectPassword()
        {
            var password = "mySecretPassword";
            var hash = _passwordHasher.Hash(password);
            
            var result = _passwordHasher.Verify(password, hash);
            
            result.Should().BeTrue();
        }

        [Fact]
        public void Verify_ShouldReturnFalseForIncorrectPassword()
        {
            var password = "mySecretPassword";
            var hash = _passwordHasher.Hash(password);
            
            var result = _passwordHasher.Verify("wrongPassword", hash);
            
            result.Should().BeFalse();
        }

        [Fact]
        public void Verify_ShouldReturnFalseForEmptyPasswordAgainstHash()
        {
            var password = "mySecretPassword";
            var hash = _passwordHasher.Hash(password);
            
            var result = _passwordHasher.Verify("", hash);
            
            result.Should().BeFalse();
        }

        [Fact]
        public void RoundTrip_HashThenVerify_ShouldWork()
        {
            var password = "RoundTripPassword123!";
            var hash = _passwordHasher.Hash(password);
            
            var isValid = _passwordHasher.Verify(password, hash);
            var isInvalid = _passwordHasher.Verify("Invalid" + password, hash);
            
            isValid.Should().BeTrue();
            isInvalid.Should().BeFalse();
        }
    }
}
