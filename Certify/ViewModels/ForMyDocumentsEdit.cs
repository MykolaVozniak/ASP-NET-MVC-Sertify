﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Certify.ViewModels
{
    public class ForMyDocumentsEdit
    {
        
        public int Id { get; set; }
        [StringLength(80, MinimumLength = 1)]
        [Required(ErrorMessage = ("Enter the file name"))]
        public string Title { get; set; }

        [StringLength(500, MinimumLength = 2)]
        [Required(ErrorMessage = ("Enter the file description"))]
        public string? ShortDescription { get; set; }
        [Required(ErrorMessage = "Select at least one user to send")]
        public string? UserEmail { get; set; }
    }
}
