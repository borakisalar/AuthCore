namespace AuthCore.Interfaces
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        public bool Verify(string password, string hashedPassword);
    }
}
