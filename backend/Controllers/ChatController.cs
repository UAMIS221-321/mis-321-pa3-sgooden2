using Microsoft.AspNetCore.Mvc;
using MovieNight.DTOs;
using MovieNight.Services;

namespace MovieNight.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly RagService _rag;
    private readonly AnthropicService _anthropic;

    public ChatController(RagService rag, AnthropicService anthropic)
    {
        _rag = rag;
        _anthropic = anthropic;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // Step 1 — RAG: retrieve relevant movies from MySQL based on the user's message
        var relevantMovies = await _rag.RetrieveRelevantMoviesAsync(request.Message);
        var context = _rag.BuildContext(relevantMovies);

        // Step 2 — LLM + Function Calling: send to Claude with RAG context and tools defined
        var reply = await _anthropic.ChatAsync(context, request.History, request.Message);

        return Ok(new ChatResponse { Reply = reply });
    }
}
