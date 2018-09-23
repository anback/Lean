// @flow
type Bar = {time: number, open:number, high:number,	low:number,	close:number,	volume:number, orderflow: number}

const TEN_MILLION = 10000000
const SOMETHING_HIGH = TEN_MILLION

export default class {
  resolution: Resolution
  bars: Object
  bars = {}
  constructor(resolution: Resolution) { this.resolution = resolution }

  onNewTrade = ([time, price, volume, side]: Array<number>, {type, date}: Object) => {
    let key = this.getKey(time)
    let bar = this.bars[key] || {time: 0, open: 0, high: 0, low: SOMETHING_HIGH, close: 0, volume: 0, orderflow: 0}

    bar = (bar: Bar)

    price = parseFloat(price)
    volume = parseFloat(volume)

    bar.time = key
    bar.open = bar.open || price
    bar.high = Math.max(bar.high, price)
    bar.low = Math.min(bar.low, price)
    bar.close = price
    bar.volume += volume
    bar.orderflow +=  side === 'Buy' ? volume : -volume

    if(!bar.open) console.log(price, volume, date, type)
    if(!bar.close) console.log(price, volume, date, type)

    this.bars[key] = bar
  }

  getKey = (time: number):number => Math.floor(time / this.resolution.value) * this.resolution.value

  getBars = (): Array<Array<number>> => Object.keys(this.bars).map(key => this.bars[key]).map(bar => [bar.time, bar.open, bar.high, bar.low, bar.close, bar.volume, bar.orderflow])
}
