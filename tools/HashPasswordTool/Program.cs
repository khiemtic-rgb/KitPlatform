if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/HashPasswordTool -- <password>");
    return 1;
}

Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(args[0]));
return 0;
