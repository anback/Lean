//@flow
import JSZip from 'jszip'
import JSZipUtils from 'jszip-utils'
import moment from 'moment'
import {ONE_MINUTE} from './server/Consts';

const BASE_URL = 'http://localhost:9000'
const COLUMN_NAMES = ['time', 'open', 'high', 'low', 'close', 'volume']
const getTradeDataUrl = (date: string) => `${BASE_URL}/minute/xbtusd/${date}_trade.zip`
const getBacktestResultsUrl = () => `${BASE_URL}/backtest.json`
const FORMAT = 'YYYYMMDD'

let getData = (): Promise<Array<DataRow>> =>
  fetch(getBacktestResultsUrl()).then(res => res.json())
  .then(backtestDataPoints => {
    let from = moment(Math.min(...backtestDataPoints.map(({Key}) => moment(Key).valueOf()))).format(FORMAT)
    let to = moment(Math.max(...backtestDataPoints.map(({Key}) => moment(Key).valueOf()))).format(FORMAT)
    return getTradeBars(from, to).then((tradeBars) => ({tradeBars, backtestDataPoints}))
  })
  .then(({tradeBars, backtestDataPoints}) => {
    tradeBars.forEach(tradeBar => {
      let tradeBarTimetamp = moment(tradeBar.date).valueOf()
      let backtestDataPoint = backtestDataPoints.find(backtestDataPoint => {
        let backtestDatapointTimetamp = Math.round(moment(backtestDataPoint.Key.replace('Z', '')).valueOf() / ONE_MINUTE) * ONE_MINUTE
        return moment(tradeBar.date).valueOf() === backtestDatapointTimetamp
      })

      tradeBar.backtestValue = backtestDataPoint ? backtestDataPoint.Value : 0
    })

    return tradeBars
  })

let getTradeBars = (from: string, to: string) => {
  let dates = []
  for (let date = moment(from); date.valueOf() <= moment(to).valueOf(); date = date.add(1, 'd')) dates.push(date.format(FORMAT))
  return Promise.all(dates.map(getTradeBarsForDate)).then(array => {
    return array.reduce((a,b) => a.concat(b), [])
  })
}

let getTradeBarsForDate = (date: string) =>
  new JSZip.external.Promise((resolve, reject) =>
    JSZipUtils.getBinaryContent(getTradeDataUrl(date), (err, data) => {
        if (err) return reject(err)
        resolve(JSZip.loadAsync(data))
  }))
  .then(data => data.files[`${date}.csv`].async("text"))
  .then(text => text.split('\n').map(line => line.split(',').reduce((a,b, i) => ({...a, [COLUMN_NAMES[i]] : parseFloat(b)}), {})))
  .then((rows) => rows.map(({time, open, high, low, close, volume}) => ({
    close,
    date: moment(moment(date).valueOf() + time).toDate(),
    high,
    low,
    open,
    absoluteChange: null,
    dividend: '',
    percentChange: undefined,
    split: '',
    volume
  })))


// export let getMinDate = (dates: Array<string>) => moment(Math.min(dates.map(date => moment(date).valueOf()))).format(FORMAT)

export default getData
