// Opens and closes a native <dialog> as a modal (OlDialog). The browser provides focus trapping, inert background,
// Esc to close and focus return; nothing else is needed.
export function open(dialog) {
    if (dialog && !dialog.open) dialog.showModal();
}

export function close(dialog) {
    if (dialog && dialog.open) dialog.close();
}
