type AlarmListener = () => void

let audioCtx: AudioContext | null = null
let oscillator: OscillatorNode | null = null
let gainNode: GainNode | null = null
let sirenTimer: number | null = null
let looping = false
let pendingPlay = false
let armed = false
let unlockBound = false
const listeners = new Set<AlarmListener>()

function notify() {
  for (const listener of listeners) {
    listener()
  }
}

export function subscribeSosAlarm(listener: AlarmListener) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function isSosAudioArmed() {
  return armed
}

export function isSosAlarmPlaying() {
  return looping && !!oscillator
}

function ensureAudioContext() {
  const AC = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!AC) {
    throw new Error('Web Audio is not supported in this browser.')
  }
  if (!audioCtx) {
    audioCtx = new AC()
  }
  return audioCtx
}

async function resumeContext() {
  const ctx = ensureAudioContext()
  if (ctx.state === 'suspended') {
    await ctx.resume()
  }
  return ctx
}

function stopSynth() {
  if (sirenTimer != null) {
    window.clearInterval(sirenTimer)
    sirenTimer = null
  }
  try {
    oscillator?.stop()
  } catch {
    /* ignore */
  }
  try {
    oscillator?.disconnect()
  } catch {
    /* ignore */
  }
  try {
    gainNode?.disconnect()
  } catch {
    /* ignore */
  }
  oscillator = null
  gainNode = null
}

function startSynthSiren(ctx: AudioContext) {
  stopSynth()
  const osc = ctx.createOscillator()
  const gain = ctx.createGain()
  osc.type = 'sawtooth'
  osc.frequency.value = 920
  gain.gain.value = 0.28
  osc.connect(gain)
  gain.connect(ctx.destination)
  osc.start()
  oscillator = osc
  gainNode = gain

  let high = true
  sirenTimer = window.setInterval(() => {
    high = !high
    if (oscillator && audioCtx) {
      oscillator.frequency.setTargetAtTime(high ? 980 : 520, audioCtx.currentTime, 0.015)
    }
  }, 380)
}

/** Silent unlock from any click so later SOS can ring without another prompt. */
export async function unlockSosAudio(): Promise<boolean> {
  try {
    const ctx = await resumeContext()
    if (ctx.state !== 'running') {
      return false
    }
    armed = true
    notify()
    if (pendingPlay) {
      pendingPlay = false
      await playSosAlarm()
    }
    return true
  } catch {
    return false
  }
}

/** Must be called from a user click/tap. Plays a short confirm beep. */
export async function armSosAudio(): Promise<boolean> {
  const ok = await unlockSosAudio()
  if (!ok || !audioCtx) {
    armed = false
    notify()
    return false
  }
  try {
    const ctx = audioCtx
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.type = 'square'
    osc.frequency.value = 880
    gain.gain.value = 0.12
    osc.connect(gain)
    gain.connect(ctx.destination)
    osc.start()
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.18)
    osc.stop(ctx.currentTime + 0.2)
    return true
  } catch {
    return armed
  }
}

export function bindSosAudioUnlock() {
  if (unlockBound) {
    return
  }
  unlockBound = true
  const unlock = () => {
    void unlockSosAudio().then((ok) => {
      if (ok) {
        window.removeEventListener('pointerdown', unlock)
        window.removeEventListener('keydown', unlock)
        window.removeEventListener('touchstart', unlock)
        unlockBound = false
      }
    })
  }
  window.addEventListener('pointerdown', unlock)
  window.addEventListener('keydown', unlock)
  window.addEventListener('touchstart', unlock, { passive: true })
}

export async function playSosAlarm() {
  looping = true
  pendingPlay = false
  notify()

  try {
    const ctx = await resumeContext()
    if (ctx.state !== 'running') {
      pendingPlay = true
      looping = false
      bindSosAudioUnlock()
      notify()
      return
    }
    armed = true
    startSynthSiren(ctx)
    notify()
  } catch {
    pendingPlay = true
    looping = false
    stopSynth()
    bindSosAudioUnlock()
    notify()
  }
}

export function stopSosAlarm() {
  looping = false
  pendingPlay = false
  stopSynth()
  notify()
}

export function isSosAlarmPending() {
  return pendingPlay
}
