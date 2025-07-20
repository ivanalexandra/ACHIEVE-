using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Achieve_Plus.Models; 

namespace Achieve_Plus.Models
{
    public class AssignmentDetailsViewModel
{
    public Assignment Assignment { get; set; }
    public UserSubmission UserSubmission { get; set; }
}

}