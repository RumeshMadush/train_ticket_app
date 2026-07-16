using Microsoft.AspNetCore.Mvc;
using TrainTicketApp.Models;
using TrainTicketApp.Services;

namespace TrainTicketApp.Controllers;

public sealed class ChatbotController(ChatbotService chatbot) : Controller
{
    public IActionResult Index()
    {
        var history=HttpContext.Session.GetObjectFromJson<List<ChatMessage>>("ChatHistory")??[];
        if(history.Count==0){history.Add(new ChatMessage{Role="bot",Text="👋 Hello! I use generated booking occurrences and reserved seats to predict prices and availability. Try asking about a route, a day, or the busiest travel pattern."});HttpContext.Session.SetObjectAsJson("ChatHistory",history);}
        return View(history);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult Send(string message){if(string.IsNullOrWhiteSpace(message))return RedirectToAction(nameof(Index));var history=HttpContext.Session.GetObjectFromJson<List<ChatMessage>>("ChatHistory")??[];history.Add(new ChatMessage{Role="user",Text=message.Trim()});history.Add(new ChatMessage{Role="bot",Text=chatbot.Reply(message)});HttpContext.Session.SetObjectAsJson("ChatHistory",history);return RedirectToAction(nameof(Index));}
    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult Clear(){HttpContext.Session.Remove("ChatHistory");return RedirectToAction(nameof(Index));}
}
