using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Microsoft.AspNetCore.Components.EventCallback;
using ErrorEventArgs = Microsoft.AspNetCore.Components.Web.ErrorEventArgs;

namespace TheBlazorState.Extensions;

/// <summary>
/// Extension methods on <see cref="ComponentBase"/> that simplify creating
/// <see cref="EventCallback{TValue}"/> instances for DOM events.
/// <para>
/// Instead of <c>EventCallback.Factory.Create&lt;MouseEventArgs&gt;(this, handler)</c>,
/// write <c>this.OnClick(handler)</c>.
/// </para>
/// </summary>
public static class EventCallbackExtensions
{
    extension(ComponentBase component)
    {
        // ── Generic fallback ────────────────────────────────────────────

        /// <summary>Creates an <see cref="EventCallback{T}"/> for any event type, ignoring the event args.</summary>
        /// <typeparam name="T">The <see cref="EventArgs"/> subtype for the DOM event.</typeparam>
        /// <param name="callback">A synchronous callback that ignores the event args.</param>
        /// <returns>An <see cref="EventCallback{T}"/> bound to this component.</returns>
        public EventCallback<T> Callback<T>(Action callback)
            where T : EventArgs
            => Factory.Create<T>(component, callback);

        /// <summary>Creates an <see cref="EventCallback{T}"/> for any event type, ignoring the event args.</summary>
        /// <typeparam name="T">The <see cref="EventArgs"/> subtype for the DOM event.</typeparam>
        /// <param name="callback">An asynchronous callback that ignores the event args.</param>
        /// <returns>An <see cref="EventCallback{T}"/> bound to this component.</returns>
        public EventCallback<T> Callback<T>(Func<Task> callback)
            where T : EventArgs
            => Factory.Create<T>(component, callback);

        /// <summary>Creates an <see cref="EventCallback{T}"/> for any event type, receiving the event args.</summary>
        /// <typeparam name="T">The <see cref="EventArgs"/> subtype for the DOM event.</typeparam>
        /// <param name="callback">A synchronous callback that receives the event args.</param>
        /// <returns>An <see cref="EventCallback{T}"/> bound to this component.</returns>
        public EventCallback<T> Callback<T>(Action<T> callback)
            where T : EventArgs
            => Factory.Create(component, callback);

        /// <summary>Creates an <see cref="EventCallback{T}"/> for any event type, receiving the event args.</summary>
        /// <typeparam name="T">The <see cref="EventArgs"/> subtype for the DOM event.</typeparam>
        /// <param name="callback">An asynchronous callback that receives the event args.</param>
        /// <returns>An <see cref="EventCallback{T}"/> bound to this component.</returns>
        public EventCallback<T> Callback<T>(Func<T, Task> callback)
            where T : EventArgs
            => Factory.Create(component, callback);

        // ── Mouse ───────────────────────────────────────────────────────

        /// <summary>Creates an <c>onclick</c> callback that ignores the event args.</summary>
        /// <param name="callback">A synchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnClick(Action callback)
            => Factory.Create<MouseEventArgs>(component, callback);

        /// <summary>Creates an <c>onclick</c> callback that ignores the event args.</summary>
        /// <param name="callback">An asynchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnClick(Func<Task> callback)
            => Factory.Create<MouseEventArgs>(component, callback);

        /// <summary>Creates an <c>onclick</c> callback that receives the event args.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="MouseEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnClick(Action<MouseEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onclick</c> callback that receives the event args.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="MouseEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnClick(Func<MouseEventArgs, Task> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>ondblclick</c> callback that ignores the event args.</summary>
        /// <param name="callback">A synchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnDoubleClick(Action callback)
            => Factory.Create<MouseEventArgs>(component, callback);

        /// <summary>Creates an <c>ondblclick</c> callback that ignores the event args.</summary>
        /// <param name="callback">An asynchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnDoubleClick(Func<Task> callback)
            => Factory.Create<MouseEventArgs>(component, callback);

        /// <summary>Creates an <c>ondblclick</c> callback that receives the event args.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="MouseEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnDoubleClick(Action<MouseEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>ondblclick</c> callback that receives the event args.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="MouseEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="MouseEventArgs"/> bound to this component.</returns>
        public EventCallback<MouseEventArgs> OnDoubleClick(Func<MouseEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Keyboard ────────────────────────────────────────────────────

        /// <summary>Creates an <c>onkeydown</c> callback that ignores the event args.</summary>
        /// <param name="callback">A synchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyDown(Action callback)
            => Factory.Create<KeyboardEventArgs>(component, callback);

        /// <summary>Creates an <c>onkeydown</c> callback that ignores the event args.</summary>
        /// <param name="callback">An asynchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyDown(Func<Task> callback)
            => Factory.Create<KeyboardEventArgs>(component, callback);

        /// <summary>Creates an <c>onkeydown</c> callback that receives the event args.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="KeyboardEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyDown(Action<KeyboardEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onkeydown</c> callback that receives the event args.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="KeyboardEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyDown(Func<KeyboardEventArgs, Task> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onkeyup</c> callback that ignores the event args.</summary>
        /// <param name="callback">A synchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyUp(Action callback)
            => Factory.Create<KeyboardEventArgs>(component, callback);

        /// <summary>Creates an <c>onkeyup</c> callback that ignores the event args.</summary>
        /// <param name="callback">An asynchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyUp(Func<Task> callback)
            => Factory.Create<KeyboardEventArgs>(component, callback);

        /// <summary>Creates an <c>onkeyup</c> callback that receives the event args.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="KeyboardEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyUp(Action<KeyboardEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onkeyup</c> callback that receives the event args.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="KeyboardEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="KeyboardEventArgs"/> bound to this component.</returns>
        public EventCallback<KeyboardEventArgs> OnKeyUp(Func<KeyboardEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Focus ───────────────────────────────────────────────────────

        /// <summary>Creates an <c>onfocus</c> / <c>onblur</c> callback that ignores the event args.</summary>
        /// <param name="callback">A synchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="FocusEventArgs"/> bound to this component.</returns>
        public EventCallback<FocusEventArgs> OnFocus(Action callback)
            => Factory.Create<FocusEventArgs>(component, callback);

        /// <summary>Creates an <c>onfocus</c> / <c>onblur</c> callback that ignores the event args.</summary>
        /// <param name="callback">An asynchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="FocusEventArgs"/> bound to this component.</returns>
        public EventCallback<FocusEventArgs> OnFocus(Func<Task> callback)
            => Factory.Create<FocusEventArgs>(component, callback);

        /// <summary>Creates an <c>onfocus</c> / <c>onblur</c> callback that receives the event args.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="FocusEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="FocusEventArgs"/> bound to this component.</returns>
        public EventCallback<FocusEventArgs> OnFocus(Action<FocusEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onfocus</c> / <c>onblur</c> callback that receives the event args.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="FocusEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="FocusEventArgs"/> bound to this component.</returns>
        public EventCallback<FocusEventArgs> OnFocus(Func<FocusEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Change / Input ──────────────────────────────────────────────

        /// <summary>Creates an <c>onchange</c> / <c>oninput</c> callback.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="ChangeEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ChangeEventArgs"/> bound to this component.</returns>
        public EventCallback<ChangeEventArgs> OnChange(Action<ChangeEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onchange</c> / <c>oninput</c> callback.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="ChangeEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ChangeEventArgs"/> bound to this component.</returns>
        public EventCallback<ChangeEventArgs> OnChange(Func<ChangeEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Drag ────────────────────────────────────────────────────────

        /// <summary>Creates an <c>ondrag*</c> / <c>ondrop</c> callback.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="DragEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="DragEventArgs"/> bound to this component.</returns>
        public EventCallback<DragEventArgs> OnDrag(Action<DragEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>ondrag*</c> / <c>ondrop</c> callback.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="DragEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="DragEventArgs"/> bound to this component.</returns>
        public EventCallback<DragEventArgs> OnDrag(Func<DragEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Touch ───────────────────────────────────────────────────────

        /// <summary>Creates an <c>ontouch*</c> callback.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="TouchEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="TouchEventArgs"/> bound to this component.</returns>
        public EventCallback<TouchEventArgs> OnTouch(Action<TouchEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>ontouch*</c> callback.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="TouchEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="TouchEventArgs"/> bound to this component.</returns>
        public EventCallback<TouchEventArgs> OnTouch(Func<TouchEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Pointer ─────────────────────────────────────────────────────

        /// <summary>Creates an <c>onpointer*</c> callback.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="PointerEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="PointerEventArgs"/> bound to this component.</returns>
        public EventCallback<PointerEventArgs> OnPointer(Action<PointerEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onpointer*</c> callback.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="PointerEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="PointerEventArgs"/> bound to this component.</returns>
        public EventCallback<PointerEventArgs> OnPointer(Func<PointerEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Wheel ───────────────────────────────────────────────────────

        /// <summary>Creates an <c>onwheel</c> callback.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="WheelEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="WheelEventArgs"/> bound to this component.</returns>
        public EventCallback<WheelEventArgs> OnWheel(Action<WheelEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onwheel</c> callback.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="WheelEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="WheelEventArgs"/> bound to this component.</returns>
        public EventCallback<WheelEventArgs> OnWheel(Func<WheelEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Clipboard ───────────────────────────────────────────────────

        /// <summary>Creates an <c>oncopy</c> / <c>oncut</c> / <c>onpaste</c> callback that ignores the event args.</summary>
        /// <param name="callback">A synchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ClipboardEventArgs"/> bound to this component.</returns>
        public EventCallback<ClipboardEventArgs> OnClipboard(Action callback)
            => Factory.Create<ClipboardEventArgs>(component, callback);

        /// <summary>Creates an <c>oncopy</c> / <c>oncut</c> / <c>onpaste</c> callback that ignores the event args.</summary>
        /// <param name="callback">An asynchronous callback.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ClipboardEventArgs"/> bound to this component.</returns>
        public EventCallback<ClipboardEventArgs> OnClipboard(Func<Task> callback)
            => Factory.Create<ClipboardEventArgs>(component, callback);

        // ── Progress ────────────────────────────────────────────────────

        /// <summary>Creates an <c>onprogress</c> callback.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="ProgressEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ProgressEventArgs"/> bound to this component.</returns>
        public EventCallback<ProgressEventArgs> OnProgress(Action<ProgressEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onprogress</c> callback.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="ProgressEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ProgressEventArgs"/> bound to this component.</returns>
        public EventCallback<ProgressEventArgs> OnProgress(Func<ProgressEventArgs, Task> callback)
            => Factory.Create(component, callback);

        // ── Error ───────────────────────────────────────────────────────

        /// <summary>Creates an <c>onerror</c> callback.</summary>
        /// <param name="callback">A synchronous callback that receives <see cref="ErrorEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ErrorEventArgs"/> bound to this component.</returns>
        public EventCallback<ErrorEventArgs> OnError(Action<ErrorEventArgs> callback)
            => Factory.Create(component, callback);

        /// <summary>Creates an <c>onerror</c> callback.</summary>
        /// <param name="callback">An asynchronous callback that receives <see cref="ErrorEventArgs"/>.</param>
        /// <returns>An <see cref="EventCallback{T}"/> of <see cref="ErrorEventArgs"/> bound to this component.</returns>
        public EventCallback<ErrorEventArgs> OnError(Func<ErrorEventArgs, Task> callback)
            => Factory.Create(component, callback);
    }
}
