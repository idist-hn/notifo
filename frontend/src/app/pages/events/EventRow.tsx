/*
 * Notifo.io
 *
 * @license
 * Copyright (c) Sebastian Stehle. All rights reserved.
 */

import classNames from 'classnames';
import * as React from 'react';
import { Button } from 'reactstrap';
import { FormatDate, Icon, JsonDetails } from '@app/framework';
import { EventDto } from '@app/service';
import { CounterRow } from '@app/shared/components';

export interface EventRowProps {
    // The event.
    event: EventDto;

    // True to show all counters.
    showCounters?: boolean;
}

export const EventRow = React.memo((props: EventRowProps) => {
    const { event, showCounters } = props;

    const [isOpen, setIsOpen] = React.useState(false);

    return (
        <>
            <CounterRow counters={event.counters} columnCount={4} showCounters={showCounters}>
                <>
                    <tr className={classNames('list-item-summary', { expanded: isOpen })}>
                        <td>
                            <Button size='sm' color='link' onClick={() => setIsOpen(!isOpen)}>
                                <Icon type={isOpen ? 'expand_less' : 'expand_more'} />
                            </Button>
                        </td>
                        <td>
                            <span className='truncate'>{event.displayName}</span>
                        </td>
                        <td>
                            <span className='truncate mono'>{event.topic}</span>
                        </td>
                        <td>
                            <span className='truncate'>
                                <FormatDate format='Ppp' date={event.created} />
                            </span>
                        </td>
                    </tr>

                    {isOpen &&
                        <tr className='list-item-details event-row-details'>
                            <td className='no-padding' colSpan={4}>
                                <JsonDetails object={event} />
                            </td>
                        </tr>
                    }
                </>
            </CounterRow>
        </>
    );
});
