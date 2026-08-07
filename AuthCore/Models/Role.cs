using System.Text.Json.Serialization;

namespace AuthCore.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonIgnore]
        public ICollection<User> Users { get; set; }
    }
}
