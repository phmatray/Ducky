// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageMessage
{
    private string Message
        => State.Message;

    private int MessageLength
        => State.SelectMessageLength();

    private string MessageInReverse
        => State.SelectMessageInReverse();

    private string MessageInUpperCase
        => State.SelectMessageInUpperCase();

    private void SetMessage()
    {
        Dispatcher.SetMessage("New message from Blazor!");
    }

    private void AppendMessage()
    {
        Dispatcher.AppendMessage(" Appended text");
    }

    private void ClearMessage()
    {
        Dispatcher.ClearMessage();
    }
}
