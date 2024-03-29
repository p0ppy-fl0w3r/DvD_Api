﻿using System.ComponentModel.DataAnnotations;

namespace DvD_Api.Models
{
    public class RegisterModel
    {
        [Required]
        [MaxLength(30, ErrorMessage = "Username cannot be more than 30 characters.")]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        [MaxLength(30, ErrorMessage = "First name must be 30 characters or less.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Date of birth is required.")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Gender is required.")]
        public string Gender { get; set; }


        [Required]
        [MaxLength(30, ErrorMessage = "First name must be 30 characters or less.")]
        public string LastName { get; set; }
    }
}
