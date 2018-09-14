// @flow
import moment from 'moment'
type Bar = {time: number, open:number, high:number,	low:number,	close:number,	volume:number}

const ONE_SECOND = 1000
const TEN_MILLION = 10000000
const SOMETHING_HIGH = TEN_MILLION

export default class {
  resolution: number
  bars: Object
  bars = {}
  constructor(resolution: number) { this.resolution = resolution }

  onNewTrade = ([time, price, volume]: Array<number>, {type, date}: Object) => {

    if(!time) return
    if(!price) return
    if(!volume) return
    if(volume === '') return
    if(price === '') return
    if(time === '') return

    let key = this.getKey(time)
    let bar = this.bars[key] || {time: 0, open: undefined, high: 0, low: SOMETHING_HIGH, close: undefined, volume: 0}

    price = parseFloat(price)
    volume = parseFloat(volume)

    bar.time = key
    bar.open = bar.open || price
    bar.high = Math.max(bar.high, price)
    bar.low = Math.min(bar.low, price)
    bar.close = price
    bar.volume +=  volume

    if(!bar.open) console.log(price, volume, date, type)
    if(!bar.close) console.log(price, volume, date, type)

    this.bars[key] = bar
  }

  getKey = (time: number):number => Math.floor(time / this.resolution) * this.resolution

  getBars = (): Array<Array<number>> => Object.keys(this.bars).map(key => this.bars[key]).map(bar => [bar.time, bar.open, bar.high, bar.low, bar.close, bar.volume])
}
