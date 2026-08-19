/// <reference types="vite/client" />

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

interface BarcodeDetector {
  detect(image: ImageBitmapSource): Promise<Array<{ rawValue?: string }>>
}

declare const BarcodeDetector: {
  new (options?: { formats: string[] }): BarcodeDetector
}

interface WindowEventMap {
  beforeinstallprompt: BeforeInstallPromptEvent
}

