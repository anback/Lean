//@flow
import React from 'react';
import { render } from 'react-dom';
import Chart from './Chart';
import getData from './getData'

class ChartComponent extends React.Component<{}, {data: Object}> {
	componentDidMount() {
		getData().then(data => { console.log('data[0]', data[0]); this.setState({ data })})
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
