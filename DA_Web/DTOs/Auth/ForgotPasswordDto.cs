﻿
using System.ComponentModel.DataAnnotations;

namespace DA_Web.DTOs.Auth
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}