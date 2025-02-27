/*
 * Notifo.io
 *
 * @license
 * Copyright (c) Sebastian Stehle. All rights reserved.
 */

import * as React from 'react';
import { match, NavLink } from 'react-router-dom';
import { Badge, Card, CardBody, DropdownItem, DropdownMenu, DropdownToggle, UncontrolledDropdown } from 'reactstrap';
import { Confirm, FormatDate, Icon } from '@app/framework';
import { ChannelTemplateDto } from '@app/service';
import { texts } from '@app/texts';
import { combineUrl } from '@sdk/shared';

export interface MessagingTemplateCardProps {
    // The match.
    match: match;

    // The template.
    template: ChannelTemplateDto;

    // The delete event.
    onDelete: (template: ChannelTemplateDto) => void;
}

export const MessagingTemplateCard = (props: MessagingTemplateCardProps) => {
    const { onDelete, match, template } = props;

    const doDelete = React.useCallback(() => {
        onDelete(template);
    }, [onDelete, template]);

    const url = combineUrl(match.url, template.id);

    return (
        <NavLink className='card-link' to={url}>
            <Card className='messaging-template'>
                <CardBody>
                    {template.name ? (
                        <h4 className='truncate'>{template.name}</h4> 
                    ) : ( 
                        <h4 className='truncate text-muted'>{texts.common.noName}</h4>
                    )}

                    {template.primary &&
                        <Badge color='primary' pill>{texts.common.primary}</Badge>
                    }

                    <div className='updated'>
                        <small><FormatDate date={template.lastUpdate} /></small>
                    </div>

                    <Confirm onConfirm={doDelete} text={texts.emailTemplates.confirmDelete}>
                        {({ onClick }) => (
                            <UncontrolledDropdown>
                                <DropdownToggle size='sm' nav>
                                    <Icon type='more' />
                                </DropdownToggle>
                                <DropdownMenu right>
                                    <DropdownItem onClick={onClick}>
                                        {texts.common.delete}
                                    </DropdownItem>
                                </DropdownMenu>
                            </UncontrolledDropdown>
                        )}
                    </Confirm>
                </CardBody>
            </Card>
        </NavLink>
    );
};
