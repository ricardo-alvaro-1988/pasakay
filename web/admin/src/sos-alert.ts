let audio: HTMLAudioElement | null = null
let unlocked = false
let looping = false

function ensureAudio() {
  if (!audio) {
    audio = new Audio('/sos-alarm.wav')
    audio.preload = 'auto'
    audio.loop = true
  }
  return audio
}

export function unlockSosAudio() {
  if (unlocked) {
    return
  }
  const clip = ensureAudio()
  clip.volume = 0
  void clip.play().then(() => {
    clip.pause()
    clip.currentTime = 0
    clip.volume = 1
    unlocked = true
  }).catch(() => {})
}

export function bindSosAudioUnlock() {
  const unlock = () => {
    unlockSosAudio()
    window.removeEventListener('pointerdown', unlock)
    window.removeEventListener('keydown', unlock)
  }
  window.addEventListener('pointerdown', unlock, { once: true })
  window.addEventListener('keydown', unlock, { once: true })
}

export function playSosAlarm() {
  const clip = ensureAudio()
  clip.currentTime = 0
  looping = true
  void clip.play().catch(() => {
    unlocked = false
  })
}

export function stopSosAlarm() {
  looping = false
  if (!audio) {
    return
  }
  audio.pause()
  audio.currentTime = 0
}

export function isSosAlarmPlaying() {
  return looping && !!audio && !audio.paused
}
