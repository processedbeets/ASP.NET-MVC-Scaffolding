using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExampleMVC.Models
{
    [Table("Examples")]
    public class Example
    {
        [Key]
        public Guid ExampleId { get; set; }
        public string Description { get; set; }
    }
}