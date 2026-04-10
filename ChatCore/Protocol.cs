using System;

namespace ChatCore;

public static class Protocol
{
    // Команды от клиента к серверу
    public const string CmdJoin  = "/join";
    public const string CmdUsers = "/users";
    public const string CmdPm    = "/pm";

    // Префиксы от сервера к клиенту
    public const string PrefixSys     = "SYS:";
    public const string PrefixUsers   = "USERS:";
    public const string PrefixPm      = "[PM от ";
    public const string PrefixWelcome = "WELCOME:";
    public const string PrefixErr     = "ERR:";

    public static string Welcome(string nick)
    {
        return $"{PrefixWelcome}{nick}";
    }

    public static string SysJoined(string nick)
    {
        return $"{PrefixSys} {nick} вошёл в чат.";
    }

    public static string SysLeft(string nick)
    {
        return $"{PrefixSys} {nick} покинул чат.";
    }

    public static string UsersList(string joined)
    {
        return $"{PrefixUsers}{joined}";
    }

    public static string ChatMessage(string nick, string text)
    {
        return $"{nick}:{text}";
    }

    public static string PrivateMessage(string from, string text)
    {
        return $"{PrefixPm}{from}]:{text}";
    }

    public static string Error(string text)
    {
        return $"{PrefixErr} {text}";
    }

    public static bool TryParseJoin(string line, out string nick)
    {
        nick = string.Empty;

        if (!line.StartsWith(CmdJoin + " "))
        {
            return false;
        }

        nick = line[(CmdJoin.Length + 1)..].Trim();
        return nick.Length > 0;
    }

    public static bool TryParsePm(string line, out string target, out string text)
    {
        target = string.Empty;
        text   = string.Empty;

        if (!line.StartsWith(CmdPm + " "))
        {
            return false;
        }

        string rest = line[(CmdPm.Length + 1)..];
        int spaceIdx = rest.IndexOf(' ');

        if (spaceIdx < 1)
        {
            return false;
        }

        target = rest[..spaceIdx].Trim();
        text   = rest[(spaceIdx + 1)..].Trim();

        return target.Length > 0 && text.Length > 0;
    }
}