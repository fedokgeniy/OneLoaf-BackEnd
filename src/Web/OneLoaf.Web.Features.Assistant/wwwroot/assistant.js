// Keeps the newest chat message in view.
export function scrollToEnd(element) {
    element?.scrollIntoView({ block: 'end', behavior: 'smooth' });
}
