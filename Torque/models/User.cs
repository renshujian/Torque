using Microsoft.AspNetCore.Identity;

namespace Torque
{
    public class User : IdentityUser
    {
        public User(string userName) : base(userName) { }
        public bool IsInRole(string role) => role == UserName;
    }
}
