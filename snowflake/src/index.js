//@flow
import React from 'react';
import { render } from 'react-dom';
import Chart from './Chart';
import { getData } from "./utils"
import {getDataForDate} from './XBTUSDDataRepository'

getDataForDate("20180907").then(console.log)

class ChartComponent extends React.Component<{}, {data: Object}> {
	componentDidMount() {
		getData().then(data => {
			console.log(data)
			this.setState({ data })
		})
	}
	render() {
		if (this.state == null) {
			return <div>Loading...</div>
		}
		return <Chart type={"hybrid"} data={this.state.data} />
	}
}

//$FlowFixMe
render(<ChartComponent />, document.getElementById("root"));
