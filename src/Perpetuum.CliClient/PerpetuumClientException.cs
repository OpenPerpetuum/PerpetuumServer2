using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient
{
    public class PerpetuumClientException : Exception
    {
        public ErrorCodes ErrorCode { get; }
        public IReadOnlyDictionary<string, object>? ExtraData { get; }

        public PerpetuumClientException(ErrorCodes errorCode, string? message = null, IReadOnlyDictionary<string, object>? extraData = null)
            : base(message ?? FormatErrorMessage(errorCode, extraData))
        {
            ErrorCode = errorCode;
            ExtraData = extraData;
        }

        public static string FormatErrorMessage(ErrorCodes errorCode, IReadOnlyDictionary<string, object>? extra = null)
        {
            return errorCode switch
            {
                ErrorCodes.NoSuchUser => "Invalid username/email or password.",
                ErrorCodes.AccountNotFound => "Account not found.",
                ErrorCodes.AccountBanned => "Account is currently banned.",
                ErrorCodes.AccountHasBeenDisconnected => "Account was already logged in on another session and has been disconnected. Please try again.",
                ErrorCodes.NoSimultaneousLoginsAllowed => "Simultaneous logins are not allowed.",
                ErrorCodes.RelayIsClosedForPublic => "Server is currently in maintenance or restricted to administrators.",
                ErrorCodes.NotSignedIn => "You are not signed in.",
                ErrorCodes.AccessDenied => "Access denied for this resource or character.",
                ErrorCodes.OffensiveNick => "This character's nickname was flagged as offensive and must be renamed.",
                ErrorCodes.CharacterNotFound => "Character not found.",
                ErrorCodes.CharacterAlreadySelected => "A character is already selected in this session.",
                ErrorCodes.AccountAlreadyExists => "An account with this email already exists.",
                ErrorCodes.NoSuchCommand => "Unknown command.",
                ErrorCodes.InsufficientPrivileges => "Insufficient privileges to execute this command.",
                ErrorCodes.RequiredArgumentIsNotSpecified => "Required arguments were missing.",
                ErrorCodes.InviteOnlyServer => "This server is invite-only.",
                _ => $"Server error: {errorCode} (Code {(int)errorCode})"
            };
        }
    }
}
