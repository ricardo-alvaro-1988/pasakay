/** Compress images in the browser before upload (JPEG, resized). */

const DEFAULT_MAX_EDGE = 1600
const DEFAULT_QUALITY = 0.72
const SKIP_IF_UNDER_BYTES = 280_000

function loadImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const img = new Image()
    img.onload = () => {
      URL.revokeObjectURL(url)
      resolve(img)
    }
    img.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new Error('Could not read image.'))
    }
    img.src = url
  })
}

function canvasToBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => (blob ? resolve(blob) : reject(new Error('Could not compress image.'))),
      type,
      quality,
    )
  })
}

/**
 * Resize + re-encode as JPEG for smaller uploads.
 * Returns the original file if compression is skipped or fails.
 */
export async function compressImageFile(
  file: File,
  options?: {
    maxEdge?: number
    quality?: number
    skipIfUnderBytes?: number
  },
): Promise<File> {
  if (!file.type.startsWith('image/') || file.type === 'image/svg+xml') {
    return file
  }

  const maxEdge = options?.maxEdge ?? DEFAULT_MAX_EDGE
  const quality = options?.quality ?? DEFAULT_QUALITY
  const skipIfUnder = options?.skipIfUnderBytes ?? SKIP_IF_UNDER_BYTES

  try {
    const img = await loadImage(file)
    const scale = Math.min(1, maxEdge / Math.max(img.naturalWidth, img.naturalHeight))
    const width = Math.max(1, Math.round(img.naturalWidth * scale))
    const height = Math.max(1, Math.round(img.naturalHeight * scale))

    // Already small and not oversized — keep original (preserves PNG transparency if any).
    if (file.size <= skipIfUnder && scale >= 1 && file.type === 'image/jpeg') {
      return file
    }

    const canvas = document.createElement('canvas')
    canvas.width = width
    canvas.height = height
    const ctx = canvas.getContext('2d')
    if (!ctx) {
      return file
    }
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, width, height)
    ctx.drawImage(img, 0, 0, width, height)

    let blob = await canvasToBlob(canvas, 'image/jpeg', quality)
    // If still large, try a stronger pass.
    if (blob.size > 1_200_000) {
      blob = await canvasToBlob(canvas, 'image/jpeg', 0.58)
    }
    if (blob.size >= file.size && scale >= 1) {
      return file
    }

    const base = file.name.replace(/\.[^.]+$/, '') || 'photo'
    return new File([blob], `${base}.jpg`, { type: 'image/jpeg', lastModified: Date.now() })
  } catch {
    return file
  }
}

export async function compressImageFiles(
  ...files: Array<File | null | undefined>
): Promise<Array<File | null>> {
  return Promise.all(files.map(async (file) => (file ? compressImageFile(file) : null)))
}
