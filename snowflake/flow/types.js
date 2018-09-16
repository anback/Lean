
type DataRow = {
  close: number,
  date: Object,
  high: number,
  low: number,
  open: number,
  absoluteChange: ?number,
  dividend: string,
  percentChange: ?number,
  split: string,
  volume: number,
  backtestValue: number,
  backtestResult: number
}

type Resolution = {
  name: string,
  value: number
}
