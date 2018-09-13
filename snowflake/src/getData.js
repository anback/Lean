//@flow
import JSZip from 'jszip'
import JSZipUtils from 'jszip-utils'
import moment from 'moment'

const COLUMN_NAMES = ['time', 'open', 'high', 'low', 'close', 'volume']
const getPath = date => `http://localhost:9000/minute/xbtusd/${date}_trade.zip`

export let getDataForDate = (date: string = "20180907") =>
  new JSZip.external.Promise((resolve, reject) =>
    JSZipUtils.getBinaryContent(getPath(date), (err, data) => {
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

export default getDataForDate
