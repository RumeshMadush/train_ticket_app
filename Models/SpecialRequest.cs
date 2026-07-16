using System.ComponentModel.DataAnnotations;

namespace TrainTicketApp.Models;

public class SpecialRequest
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Booking ID")]
    public int BookingId { get; set; }

    [Display(Name = "Passenger Name")]
    public string PassengerName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Request Type")]
    public RequestType RequestType { get; set; }

    [Required]
    [Display(Name = "Description")]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Status")]
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    [Display(Name = "Submitted On")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Date Needed")]
    [DataType(DataType.Date)]
    public DateTime DateNeeded { get; set; }

    [Range(0, 100000)]
    [Display(Name = "Additional cost (Rs)")]
    public decimal AdditionalCost { get; set; }
}

public enum RequestType
{
    WheelchairAssistance,
    SpecialMeal,
    BaggageAssistance,
    SeatingPreference,
    ChildEscort,
    PetTransport,
    Other
}

public enum RequestStatus
{
    Pending,
    Approved,
    Rejected
}
