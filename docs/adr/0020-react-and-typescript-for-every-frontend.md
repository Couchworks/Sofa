# Every frontend is one React and TypeScript app

The web client is built in React and TypeScript, and desktop and mobile later wrap the same app (Tauri or Electron for desktop, Capacitor for mobile) rather than being written again. Its API client is generated from the OpenAPI description, so the documented API stays the only contract between frontends and the backend. React has the widest ecosystem, and agents write it most fluently, which counts once Sofa's own frontend is built by Runs.

## Considered Options

- **Blazor, with Blazor Hybrid in MAUI for desktop and mobile**: one language across the product and request types shared directly, but a smaller ecosystem, large WebAssembly downloads, MAUI's uneven record, and agents less fluent in it.
- **Uno Platform or Avalonia**: native desktop and mobile in C#, but web, which comes first, is their weakest target.
