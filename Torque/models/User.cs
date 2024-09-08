using Microsoft.AspNetCore.Identity;
using System;

namespace Torque
{
    public class User : IdentityUser
    {
        public User(string userName) : base(userName) { }
        public bool IsInRole(string role) => role == UserName;
        public bool IsAdmin => IsInRole("管理员");
        public DateTime LastLoginTime { get; set; }
    }
}
