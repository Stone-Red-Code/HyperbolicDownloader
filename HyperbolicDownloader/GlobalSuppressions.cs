// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Critical Code Smell", "S3998:Threads should not lock on objects with weak identity", Justification = "Not a problem in this case", Scope = "member", Target = "~M:HyperbolicDownloader.LogCommands.OnNotificationMessageRecived(System.Object,HyperbolicDownloaderApi.Managment.NotificationMessageEventArgs)")]