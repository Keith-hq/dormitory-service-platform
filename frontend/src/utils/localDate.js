const pad = (value) => String(value).padStart(2, '0')

export const formatLocalDateInput = (date = new Date()) =>
  `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`

export const formatLocalMonthInput = (date = new Date()) =>
  `${date.getFullYear()}-${pad(date.getMonth() + 1)}`

export const formatLocalDateTimeInput = (date = new Date()) =>
  `${formatLocalDateInput(date)}T${pad(date.getHours())}:${pad(date.getMinutes())}`
