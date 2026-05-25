using System;
using System.Collections.Generic;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Auth
{
    public class OnboardingProfileData
    {
        public string Name        { get; set; }
        public string Surname     { get; set; }
        public DateTime DateOfBirth { get; set; }
        public CultureType Culture  { get; set; }
        public List<HobbyType> Hobbies { get; set; } = new List<HobbyType>();
    }
}
