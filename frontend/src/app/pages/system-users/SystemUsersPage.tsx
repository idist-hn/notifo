/*
 * Notifo.io
 *
 * @license
 * Copyright (c) Sebastian Stehle. All rights reserved.
 */

import * as React from 'react';
import { useDispatch } from 'react-redux';
import ReactTooltip from 'react-tooltip';
import { Button, Col, Row, Table } from 'reactstrap';
import { FormError, Icon, ListSearch, Loader, Query, useDialog } from '@app/framework';
import { SystemUserDto } from '@app/service';
import { TableFooter } from '@app/shared/components';
import { deleteSystemUser, loadSystemUsers, lockSystemUser, unlockSystemUser, useSystemUsers } from '@app/state';
import { texts } from '@app/texts';
import { SystemUserDialog } from './SystemUserDialog';
import { SystemUserRow } from './SystemUserRow';

export const SystemUsersPage = () => {
    const dispatch = useDispatch();
    const dialogEdit = useDialog();
    const dialogNew = useDialog();
    const systemUsers = useSystemUsers(x => x.systemUsers);
    const [currentSystemUser, setCurrentSystemUser] = React.useState<SystemUserDto>();

    React.useEffect(() => {
        ReactTooltip.rebuild();
    });

    React.useEffect(() => {
        dispatch(loadSystemUsers({}));
    }, [dispatch]);

    const doRefresh = React.useCallback(() => {
        dispatch(loadSystemUsers());
    }, [dispatch]);

    const doLoad = React.useCallback((q?: Partial<Query>) => {
        dispatch(loadSystemUsers(q));
    }, [dispatch]);

    const doLock = React.useCallback((user: SystemUserDto) => {
        dispatch(lockSystemUser({ userId: user.id }));
    }, [dispatch]);

    const doUnlock = React.useCallback((user: SystemUserDto) => {
        dispatch(unlockSystemUser({ userId: user.id }));
    }, [dispatch]);

    const doDelete = React.useCallback((user: SystemUserDto) => {
        dispatch(deleteSystemUser({ userId: user.id }));
    }, [dispatch]);

    const doEdit = React.useCallback((systemUser: SystemUserDto) => {
        setCurrentSystemUser(systemUser);

        dialogEdit.open();
    }, [dialogEdit]);

    return (
        <main className='pl-4'>
            <div className='users'>
                <Row className='align-items-center header'>
                    <Col xs={12} md={5}>
                        <Row className='align-items-center flex-nowrap'>
                            <Col>
                                <h2 className='truncate'>{texts.systemUsers.header}</h2>
                            </Col>
                            <Col xs='auto' className='col-refresh'>
                                {systemUsers.isLoading ? (
                                    <Loader visible={systemUsers.isLoading} />
                                ) : (
                                    <Button color='blank' size='sm' className='btn-flat' onClick={doRefresh} data-tip={texts.common.refresh}>
                                        <Icon className='text-lg' type='refresh' />
                                    </Button>
                                )}
                            </Col>
                        </Row>
                    </Col>
                    <Col xs={12} md={7}>
                        <Row noGutters className='flex-nowrap'>
                            <Col>
                                <ListSearch list={systemUsers} onSearch={doLoad} placeholder={texts.systemUsers.searchPlaceholder} />
                            </Col>
                            <Col xs='auto pl-2'>
                                <Button color='success' onClick={dialogNew.open}>
                                    <Icon type='add' /> {texts.users.createButton}
                                </Button>
                            </Col>
                        </Row>
                    </Col>
                </Row>

                <FormError error={systemUsers.error} />

                <Table className='table-fixed table-simple table-middle'>
                    <colgroup>
                        <col />
                        <col style={{ width: 100 }} />
                        <col style={{ width: 300 }} />
                    </colgroup>

                    <thead>
                        <tr>
                            <th>
                                <span className='truncate'>{texts.common.email}</span>
                            </th>
                            <th>
                                <span className='truncate'>{texts.common.admin}</span>
                            </th>
                            <th className='text-right'>
                                <span className='truncate'>{texts.common.actions}</span>
                            </th>
                        </tr>
                    </thead>

                    <tbody>
                        {systemUsers.items &&
                            <>
                                {systemUsers.items.map(systemUser => (
                                    <SystemUserRow key={systemUser.id} user={systemUser}
                                        onDelete={doDelete}
                                        onEdit={doEdit}
                                        onLock={doLock}
                                        onUnlock={doUnlock}
                                    />
                                ))}
                            </>
                        }
                    </tbody>
                </Table>

                <TableFooter noDetailButton list={systemUsers}
                    onChange={doLoad} />

                {dialogNew.isOpen &&
                    <SystemUserDialog onClose={dialogNew.close} />
                }

                {dialogEdit.isOpen &&
                    <SystemUserDialog user={currentSystemUser} onClose={dialogEdit.close} />
                }
            </div>
        </main>
    );
};
