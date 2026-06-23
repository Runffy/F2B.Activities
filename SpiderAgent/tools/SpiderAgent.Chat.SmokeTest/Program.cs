using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Extensions;
using SpiderAgent.Chat.Models;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: false);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddSpiderAgentChat(context.Configuration.GetSection("Chat"));
    })
    .Build();

var sessionFactory = host.Services.GetRequiredService<IChatSessionFactory>();
var session = sessionFactory.Create(new ChatSessionOptions
{
    SystemPrompt = "你是 SpiderAgent 测试助手。请用中文简短回答。",
    CompletionOptions = new ChatCompletionOptions
    {
        Temperature = 0.7,
        MaxTokens = 200,
        Stream = false
    }
});

Console.WriteLine("=== SpiderAgent Chat 冒烟测试 ===");
Console.WriteLine($"deepseekapikey 环境变量: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("deepseekapikey")) ? "未设置" : "已设置")}");
Console.WriteLine();

// 测试 1：单次 Complete
Console.WriteLine("[测试 1] 单次对话 (CompleteAsync)");
var response1 = await session.SendAsync("请用一句话介绍你自己。");
Console.WriteLine($"回复: {response1.Content}");
Console.WriteLine($"Token: prompt={response1.Usage?.PromptTokens}, completion={response1.Usage?.CompletionTokens}, total={response1.Usage?.TotalTokens}");
Console.WriteLine($"历史消息数: {session.History.Count}");
Console.WriteLine();

// 测试 2：多轮历史
Console.WriteLine("[测试 2] 多轮对话 (验证 history)");
var response2 = await session.SendAsync("我上一条问了你什么？请直接引用我的原话。");
Console.WriteLine($"回复: {response2.Content}");
Console.WriteLine($"历史消息数: {session.History.Count}");
Console.WriteLine();

// 测试 3：流式
Console.WriteLine("[测试 3] 流式输出 (StreamSendAsync)");
Console.Write("回复: ");
await foreach (var chunk in session.StreamSendAsync("用不超过20字说'流式测试成功'。"))
{
    if (!string.IsNullOrEmpty(chunk.Delta))
    {
        Console.Write(chunk.Delta);
    }
}

Console.WriteLine();
Console.WriteLine($"历史消息数: {session.History.Count}");
Console.WriteLine();
Console.WriteLine("=== 全部测试完成 ===");
